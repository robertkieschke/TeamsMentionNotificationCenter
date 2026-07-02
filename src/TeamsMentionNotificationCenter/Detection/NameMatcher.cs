using System.Globalization;
using System.Text;
using TeamsMentionNotificationCenter.Settings;

namespace TeamsMentionNotificationCenter.Detection;

/// <summary>
/// Prüft Untertitel-Text gegen die konfigurierten Trigger-Wörter.
/// - Normalisierung: klein, ohne Diakritika, Satzzeichen -> Leerzeichen
/// - Wortweiser Vergleich mit exaktem, Präfix- und optionalem Fuzzy-Match (Levenshtein)
/// - Cooldown gegen Mehrfach-Auslösung
/// - Optionales Ignorieren eigener Redebeiträge
/// Thread-sicher für den typischen Gebrauch (ein Poll-Thread ruft <see cref="TryMatch"/>).
/// </summary>
public sealed class NameMatcher
{
    private readonly object _gate = new();
    private List<string> _normalizedTriggers = new();
    private bool _fuzzy;
    private int _fuzzyMaxDistance;
    private bool _ignoreOwnSpeaker;
    private string _ownSpeakerNormalized = "";
    private TimeSpan _cooldown;
    private DateTime _lastMatchUtc = DateTime.MinValue;

    public NameMatcher(AppSettings settings) => UpdateFrom(settings);

    public void UpdateFrom(AppSettings s)
    {
        lock (_gate)
        {
            _normalizedTriggers = s.TriggerWords
                .Select(Normalize)
                .Where(w => w.Length > 0)
                .Distinct()
                .ToList();
            _fuzzy = s.FuzzyEnabled;
            _fuzzyMaxDistance = Math.Max(0, s.FuzzyMaxDistance);
            _ignoreOwnSpeaker = s.IgnoreOwnSpeaker;
            _ownSpeakerNormalized = Normalize(s.OwnSpeakerName);
            _cooldown = TimeSpan.FromMilliseconds(Math.Max(0, s.TriggerCooldownMs));
        }
    }

    /// <summary>Setzt den Cooldown zurück (z. B. beim Wechsel des Meetings).</summary>
    public void ResetCooldown()
    {
        lock (_gate) _lastMatchUtc = DateTime.MinValue;
    }

    /// <summary>
    /// Prüft, ob <paramref name="text"/> einen Trigger enthält und der Cooldown abgelaufen ist.
    /// Gibt bei einem frischen Treffer true zurück und liefert das getroffene Wort.
    /// </summary>
    public bool TryMatch(string speaker, string text, out string matchedWord)
    {
        matchedWord = "";
        lock (_gate)
        {
            if (_normalizedTriggers.Count == 0) return false;

            if (_ignoreOwnSpeaker && _ownSpeakerNormalized.Length > 0)
            {
                var sp = Normalize(speaker);
                if (sp.Length > 0 && (sp == _ownSpeakerNormalized ||
                                      sp.Contains(_ownSpeakerNormalized) ||
                                      _ownSpeakerNormalized.Contains(sp)))
                    return false;
            }

            if (!ContainsTrigger(text, out matchedWord)) return false;

            var now = DateTime.UtcNow;
            if (now - _lastMatchUtc < _cooldown) return false; // Cooldown aktiv
            _lastMatchUtc = now;
            return true;
        }
    }

    /// <summary>Reiner Treffer-Test ohne Cooldown (für Tests/Vorschau).</summary>
    public bool ContainsTrigger(string text, out string matchedWord)
    {
        matchedWord = "";
        var tokens = Tokenize(Normalize(text));
        if (tokens.Count == 0) return false;

        foreach (var trigger in _normalizedTriggers)
        {
            foreach (var token in tokens)
            {
                if (token == trigger ||
                    (trigger.Length >= 4 && token.StartsWith(trigger, StringComparison.Ordinal)))
                {
                    matchedWord = trigger;
                    return true;
                }
                if (_fuzzy && _fuzzyMaxDistance > 0 &&
                    Math.Abs(token.Length - trigger.Length) <= _fuzzyMaxDistance &&
                    trigger.Length >= 4 &&
                    LevenshteinAtMost(token, trigger, _fuzzyMaxDistance))
                {
                    matchedWord = trigger;
                    return true;
                }
            }
        }
        return false;
    }

    // --- Normalisierung ---------------------------------------------------
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        // Diakritika entfernen
        var decomposed = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else sb.Append(' ');
        }
        // deutsche Umlaut-Sonderfälle nach Dekomposition sind bereits als Basisbuchstabe vorhanden;
        // ß -> ss für robusteren Vergleich
        return sb.ToString().Replace("ß", "ss").Normalize(NormalizationForm.FormC);
    }

    private static List<string> Tokenize(string normalized) =>
        normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

    // --- Levenshtein mit früher Abbruchgrenze ------------------------------
    internal static bool LevenshteinAtMost(string a, string b, int max)
    {
        int la = a.Length, lb = b.Length;
        if (Math.Abs(la - lb) > max) return false;
        if (la == 0) return lb <= max;
        if (lb == 0) return la <= max;

        var prev = new int[lb + 1];
        var cur = new int[lb + 1];
        for (int j = 0; j <= lb; j++) prev[j] = j;

        for (int i = 1; i <= la; i++)
        {
            cur[0] = i;
            int rowMin = cur[0];
            for (int j = 1; j <= lb; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                cur[j] = Math.Min(Math.Min(prev[j] + 1, cur[j - 1] + 1), prev[j - 1] + cost);
                if (cur[j] < rowMin) rowMin = cur[j];
            }
            if (rowMin > max) return false; // ganze Zeile über Grenze -> Abbruch
            (prev, cur) = (cur, prev);
        }
        return prev[lb] <= max;
    }
}
