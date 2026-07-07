using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TeamsMentionNotificationCenter.Detection;
using TeamsMentionNotificationCenter.Settings;

namespace TeamsMentionNotificationCenter.Core;

/// <summary>Status eines verpassten Erwähnungs-Eintrags.</summary>
public enum MentionStatus
{
    /// <summary>Offen/überfällig – wartet auf Reaktion (orange).</summary>
    Open,
    /// <summary>Zurückgestellt bis <see cref="MissedMention.SnoozeUntil"/> (grau).</summary>
    Snoozed,
    /// <summary>Wartet darauf, dass die Person wieder im Call auftaucht (grau).</summary>
    WaitingForPerson,
    /// <summary>Erledigt (grün; verschwindet aus dem Overlay, bleibt in der Liste).</summary>
    Done
}

/// <summary>Ein Eintrag „X hat dich um HH:mm gerufen und du hast nicht geantwortet".</summary>
public sealed class MissedMention
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Speaker { get; set; } = "";
    public DateTime MentionedAt { get; set; }
    public MentionStatus Status { get; set; } = MentionStatus.Open;
    public DateTime? SnoozeUntil { get; set; }
    public DateTime? DoneAt { get; set; }
}

/// <summary>
/// Persistenter Speicher der verpassten Erwähnungen (%APPDATA%\…\mentions.json).
/// Gespeichert werden NUR Sprechername, Zeitpunkt und Bearbeitungsstatus – nie Gesprächsinhalte.
/// Einträge älter als die konfigurierte Aufbewahrung werden automatisch entfernt.
/// Alle Methoden sind für den UI-Thread gedacht; <see cref="Changed"/> feuert nach jeder Änderung.
/// </summary>
public sealed class MentionStore
{
    private readonly List<MissedMention> _items = new();
    private readonly string _filePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public event EventHandler? Changed;

    public MentionStore(string? filePath = null) =>
        _filePath = filePath ?? Path.Combine(AppSettings.SettingsDirectory, "mentions.json");

    public IReadOnlyList<MissedMention> Items => _items;

    /// <summary>Anzahl nicht erledigter Einträge (offen, zurückgestellt oder wartend).</summary>
    public int UnfinishedCount => _items.Count(m => m.Status != MentionStatus.Done);

    /// <summary>Sprecher, auf deren Rückkehr gewartet wird.</summary>
    public string[] WaitingSpeakers => _items
        .Where(m => m.Status == MentionStatus.WaitingForPerson)
        .Select(m => m.Speaker)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public void Load(int retentionDays)
    {
        _items.Clear();
        try
        {
            if (File.Exists(_filePath))
            {
                var loaded = JsonSerializer.Deserialize<List<MissedMention>>(File.ReadAllText(_filePath), JsonOptions);
                if (loaded != null) _items.AddRange(loaded);
            }
        }
        catch { /* beschädigte Datei -> leer starten */ }
        Cleanup(retentionDays);
        Raise();
    }

    /// <summary>Auto-Löschung: Einträge, deren Nennung älter als <paramref name="retentionDays"/> Tage ist.</summary>
    public void Cleanup(int retentionDays)
    {
        var cutoff = DateTime.Now.Date.AddDays(-Math.Max(1, retentionDays));
        int removed = _items.RemoveAll(m => m.MentionedAt < cutoff);
        if (removed > 0) { Save(); Raise(); }
    }

    /// <summary>Neuen Eintrag anlegen – dieselbe Person aber frühestens nach <paramref name="repeatMinutes"/>
    /// Minuten erneut (verschiedene Personen immer). Liefert null, wenn verworfen.</summary>
    public MissedMention? AddIfNew(string speaker, DateTime at, int repeatMinutes)
    {
        if (string.IsNullOrWhiteSpace(speaker)) speaker = "?";
        var lastSame = _items
            .Where(m => NamesMatch(m.Speaker, speaker))
            .OrderByDescending(m => m.MentionedAt)
            .FirstOrDefault();
        if (lastSame != null && (at - lastSame.MentionedAt).TotalMinutes < Math.Max(0, repeatMinutes))
            return null;

        var entry = new MissedMention { Speaker = speaker.Trim(), MentionedAt = at };
        _items.Add(entry);
        Save();
        Raise();
        return entry;
    }

    public void MarkDone(Guid id) => Mutate(id, m => { m.Status = MentionStatus.Done; m.DoneAt = DateTime.Now; m.SnoozeUntil = null; });
    public void Reopen(Guid id) => Mutate(id, m => { m.Status = MentionStatus.Open; m.DoneAt = null; m.SnoozeUntil = null; });
    public void Snooze(Guid id, int minutes) => Mutate(id, m => { m.Status = MentionStatus.Snoozed; m.SnoozeUntil = DateTime.Now.AddMinutes(Math.Max(1, minutes)); });
    public void WaitForPerson(Guid id) => Mutate(id, m => { m.Status = MentionStatus.WaitingForPerson; m.SnoozeUntil = null; });

    public void MarkAllDone()
    {
        bool any = false;
        foreach (var m in _items.Where(m => m.Status != MentionStatus.Done))
        {
            m.Status = MentionStatus.Done;
            m.DoneAt = DateTime.Now;
            m.SnoozeUntil = null;
            any = true;
        }
        if (any) { Save(); Raise(); }
    }

    public void Delete(Guid id)
    {
        if (_items.RemoveAll(m => m.Id == id) > 0) { Save(); Raise(); }
    }

    public void DeleteDay(DateTime day)
    {
        if (_items.RemoveAll(m => m.MentionedAt.Date == day.Date) > 0) { Save(); Raise(); }
    }

    public void DeleteAll()
    {
        if (_items.Count > 0) { _items.Clear(); Save(); Raise(); }
    }

    /// <summary>Abgelaufene Zurückstellungen wieder öffnen. Liefert die geänderten Einträge.</summary>
    public List<MissedMention> TickSnoozes(DateTime now)
    {
        var due = _items.Where(m => m.Status == MentionStatus.Snoozed && m.SnoozeUntil <= now).ToList();
        foreach (var m in due) { m.Status = MentionStatus.Open; m.SnoozeUntil = null; }
        if (due.Count > 0) { Save(); Raise(); }
        return due;
    }

    /// <summary>Person wurde wieder gesehen/gehört: wartende Einträge dieser Person wieder öffnen.</summary>
    public List<MissedMention> PersonSeen(string speaker)
    {
        var hits = _items
            .Where(m => m.Status == MentionStatus.WaitingForPerson && NamesMatch(m.Speaker, speaker))
            .ToList();
        foreach (var m in hits) m.Status = MentionStatus.Open;
        if (hits.Count > 0) { Save(); Raise(); }
        return hits;
    }

    /// <summary>Toleranter Namensvergleich (wie IsOwnSpeaker): normalisiert; alle Tokens des
    /// kürzeren Namens müssen im längeren vorkommen („Robert H. Kieschke" ≙ „Robert Kieschke").</summary>
    public static bool NamesMatch(string a, string b)
    {
        var na = NameMatcher.Normalize(a ?? "");
        var nb = NameMatcher.Normalize(b ?? "");
        if (na.Length == 0 || nb.Length == 0) return false;
        if (na == nb) return true;
        var ta = na.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var tb = nb.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var (shorter, longer) = ta.Length <= tb.Length ? (ta, tb) : (tb, ta);
        return shorter.Length > 0 && shorter.All(longer.Contains) && shorter.Any(t => t.Length >= 2);
    }

    private void Mutate(Guid id, Action<MissedMention> change)
    {
        var m = _items.FirstOrDefault(x => x.Id == id);
        if (m == null) return;
        change(m);
        Save();
        Raise();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(_items, JsonOptions));
        }
        catch { /* Platte nicht beschreibbar -> Einträge leben im RAM weiter */ }
    }

    private void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}
