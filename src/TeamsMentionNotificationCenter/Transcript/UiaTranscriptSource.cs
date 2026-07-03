using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using TeamsMentionNotificationCenter.Core;
using TeamsMentionNotificationCenter.Localization;
using TeamsMentionNotificationCenter.Settings;

namespace TeamsMentionNotificationCenter.Transcript;

/// <summary>
/// Liest die Teams-Live-Untertitel per UI Automation.
///
/// In Phase 0 auf dem Zielrechner verifiziert:
///  - Chromium/WebView2 legt seinen Accessibility-Baum erst nach Aktivierung offen
///    (WM_GETOBJECT custom id 1 + OBJID_CLIENT sowie MSAA accName(0) auf die Render-Fenster).
///  - Untertitel-Zeile = &lt;Group ClassName enthält "ChatMessageCompact"&gt; mit zwei
///    &lt;Text&gt;-Kindern: [0] Sprecher, [1] gesprochener Text.
///
/// Überwacht ALLE Untertitel-Fenster gleichzeitig (mehrere/gehaltene Calls). Auslösung erfolgt
/// über die Häufigkeits-Differenz der sichtbaren Zeilen zum vorherigen Poll: eine NEU erschienene
/// Zeile (auch mit gleichem Wortlaut wie zuvor) löst aus; eine unverändert sichtbare nicht.
/// So werden wiederholte Nennungen erkannt, ohne stehende Untertitel erneut auszulösen.
///
/// Läuft auf einem eigenen MTA-Thread; Events werden aus diesem Thread gefeuert.
/// Datenschutz: Nichts wird gespeichert – Text wird nur im RAM verarbeitet und nach der Prüfung verworfen.
/// </summary>
public sealed class UiaTranscriptSource : ITranscriptSource
{
    private readonly AppSettings _settings;
    private Thread? _thread;
    private volatile bool _running;

    private Dictionary<string, int> _prevCounts = new(StringComparer.Ordinal); // Zeilen-Häufigkeit letzter Poll
    private bool _primedOnce;                              // Startbestand einmalig ignorieren
    private readonly HashSet<IntPtr> _activated = new();  // Fenster, deren A11y bereits aktiviert wurde
    private readonly HashSet<IntPtr> _loggedCallCandidates = new(); // kleine Fenster, deren Buttons schon geloggt wurden
    private bool _lastCallVisible;
    private int _pollTick;
    private bool _lastAvailable;

    public event EventHandler<CaptionEventArgs>? CaptionReceived;
    public event EventHandler<TranscriptStatusEventArgs>? StatusChanged;
    public event EventHandler<bool>? IncomingCallVisibleChanged;

    public UiaTranscriptSource(AppSettings settings) => _settings = settings;

    public void Start()
    {
        if (_running) return;
        _running = true;
        _thread = new Thread(Run) { IsBackground = true, Name = "TeamsMentionNotificationCenter-UIA" };
        _thread.SetApartmentState(ApartmentState.MTA);
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        _thread?.Join(2000);
        _thread = null;
    }

    public void Dispose() => Stop();

    private void Run()
    {
        UIA3Automation? automation = null;
        try
        {
            automation = new UIA3Automation();
            while (_running)
            {
                try { PollOnce(automation); }
                catch { SetAvailable(false, Loc.T("Fehler beim Lesen – versuche erneut …")); }
                Thread.Sleep(Math.Max(150, _settings.PollIntervalMs));
            }
        }
        catch (Exception ex)
        {
            SetAvailable(false, "UIA-Initialisierung fehlgeschlagen: " + ex.Message);
        }
        finally
        {
            automation?.Dispose();
        }
    }

    private void PollOnce(UIA3Automation automation)
    {
        var teamsPids = Process.GetProcessesByName("ms-teams").Select(p => p.Id).ToHashSet();
        if (teamsPids.Count == 0) { SetCallVisible(false); SetAvailable(false, Loc.T("Teams läuft nicht.")); return; }

        var all = Safe(() => automation.GetDesktop().FindAllChildren()) ?? Array.Empty<AutomationElement>();
        var teamsWindows = all
            .Where(e => teamsPids.Contains(Safe(() => e.Properties.ProcessId.ValueOrDefault)))
            .ToList();
        if (teamsWindows.Count == 0) { SetCallVisible(false); SetAvailable(false, Loc.T("Kein Teams-Fenster gefunden.")); return; }

        // Eingehenden Anruf erkennen (kleines Popup mit Annehmen-/Ablehnen-Buttons).
        DetectIncomingCall(teamsWindows);

        // ALLE Teams-Fenster außer dem Chat-Hub prüfen – deckt ausgekoppelte Untertitel-Fenster UND
        // In-Meeting-Untertitel ab, auch bei mehreren Calls gleichzeitig (egal ob ausgekoppelt).
        var sources = teamsWindows
            .Where(w => !IsChatHub(Safe(() => w.Properties.Name.ValueOrDefault) ?? ""))
            .ToList();

        // Aktivierungs-Merkmenge auf existierende Fenster beschränken; A11y periodisch neu anstoßen.
        var currentHwnds = sources
            .Select(w => Safe(() => w.Properties.NativeWindowHandle.ValueOrDefault))
            .Where(h => h != IntPtr.Zero)
            .ToHashSet();
        _activated.RemoveWhere(h => !currentHwnds.Contains(h));
        if (++_pollTick % 20 == 0) _activated.Clear();

        // Aktuelle Caption-Zeilen einsammeln und je (Fenster+Sprecher+Wortlaut) zählen.
        var current = new Dictionary<string, int>(StringComparer.Ordinal);
        var lines = new List<(string Key, CaptionLine Line)>();
        int totalGroups = 0;
        var pollBudget = Stopwatch.StartNew();
        foreach (var w in sources)
        {
            if (pollBudget.ElapsedMilliseconds > 6000) break; // Gesamt-Zeitbudget pro Durchlauf
            var hwnd = Safe(() => w.Properties.NativeWindowHandle.ValueOrDefault);
            if (hwnd == IntPtr.Zero) continue;

            if (_activated.Add(hwnd)) ActivateAccessibility(hwnd); // nur neu gesehene Fenster aktivieren

            // Ausgekoppelte Untertitel-Fenster sind klein; Meeting-Fenster größer -> mehr Budget.
            bool isCaptionWindow = LooksLikeCaptionWindow(Safe(() => w.Properties.Name.ValueOrDefault) ?? "");
            var groups = new List<AutomationElement>();
            CollectCaptionGroups(w, groups, 0, 60, new[] { 0 },
                maxCount: isCaptionWindow ? 6000 : 9000, Stopwatch.StartNew(),
                maxMs: isCaptionWindow ? 2500 : 3500);
            if (groups.Count == 0) continue;
            totalGroups += groups.Count;

            long h = hwnd.ToInt64();
            foreach (var g in groups)
            {
                var line = GetLineFromGroup(g);
                if (string.IsNullOrWhiteSpace(line.Text)) continue;
                string key = h + "|" + line.Speaker + "" + line.Text;
                current[key] = current.GetValueOrDefault(key) + 1;
                lines.Add((key, line));
            }
        }

        // Auslösen anhand der Häufigkeits-Differenz zum letzten Poll.
        if (!_primedOnce)
        {
            _primedOnce = true; // Startbestand einmalig übernehmen, ohne auszulösen
        }
        else
        {
            var emittedThisPoll = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (key, line) in lines)
            {
                int allowed = current[key] - _prevCounts.GetValueOrDefault(key);
                int already = emittedThisPoll.GetValueOrDefault(key);
                if (already < allowed)
                {
                    emittedThisPoll[key] = already + 1;
                    CaptionReceived?.Invoke(this, new CaptionEventArgs(line));
                }
            }
        }
        if (current.Count > 0) _prevCounts = current; // transiente Leer-Lesungen nicht als Baseline übernehmen

        SetAvailable(totalGroups > 0, totalGroups > 0
            ? Loc.Tf("Untertitel aktiv ({0} Zeilen, {1} Fenster).", totalGroups, sources.Count)
            : Loc.T("Fenster gefunden, aber keine Untertitel (sind Untertitel aktiv?)."));
    }

    // Schlüsselwörter für Untertitel-Fenster in mehreren Sprachen. Trifft der Titel nicht, greift
    // der sprachunabhängige Fallback (Caption-Zeilen werden per CSS-Klasse "fui-ChatMessageCompact"
    // gefunden – diese ist nicht lokalisiert).
    private static readonly string[] CaptionKeywords =
    {
        "caption", "beschriftung", "untertitel", "live captions", // EN/DE
        "sous-titres", "subtítulos", "subtitulos", "sottotitoli",  // FR/ES/IT
        "legendas", "ondertiteling", "napisy", "字幕"               // PT/NL/PL/CJK
    };

    private static bool LooksLikeCaptionWindow(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        name = name.ToLowerInvariant();
        foreach (var kw in CaptionKeywords)
            if (name.Contains(kw)) return true;
        return false;
    }

    private static bool IsChatHub(string name)
    {
        name = (name ?? "").ToLowerInvariant().TrimStart();
        return name.StartsWith("chat ") || name.StartsWith("chat|") || name == "chat";
    }

    // --- Eingehenden Anruf erkennen ---------------------------------------
    // Das Anruf-Popup von Teams ist ein kleines Always-on-top-Fenster mit Annehmen-/Ablehnen-
    // Buttons. Es gibt keine offizielle API – erkannt wird ein KLEINES ms-teams-Fenster, dessen
    // UIA-Baum einen Button mit entsprechender (lokalisierter) Beschriftung enthält.
    private static readonly string[] CallButtonKeywords =
    {
        "annehmen", "ablehnen",                        // DE
        "accept", "decline", "reject",                 // EN
        "accetta", "rifiuta",                          // IT
        "accepter", "refuser", "décliner",             // FR
        "aceptar", "rechazar",                         // ES
        "aceitar", "recusar",                          // PT
        "accepteren", "weigeren",                      // NL
        "odbierz", "odrzuć",                           // PL
        "応答", "拒否", "接听", "拒绝"                    // JA/ZH
    };

    private void DetectIncomingCall(List<AutomationElement> teamsWindows)
    {
        if (!_settings.EnterConversationOnIncomingCall) { SetCallVisible(false); return; }

        bool visible = false;
        foreach (var w in teamsWindows)
        {
            if (IsIncomingCallToast(w)) { visible = true; break; }
        }
        SetCallVisible(visible);
    }

    private bool IsIncomingCallToast(AutomationElement w)
    {
        // Nur kleine, sichtbare Fenster genauer ansehen (Meeting-/Hauptfenster sind groß;
        // minimierte Fenster liegen bei ca. -32000).
        var rect = Safe(() => w.Properties.BoundingRectangle.ValueOrDefault);
        if (rect.Width < 150 || rect.Height < 60 || rect.Width > 700 || rect.Height > 520) return false;
        if (rect.Left < -20000 || rect.Top < -20000) return false;

        var hwnd = Safe(() => w.Properties.NativeWindowHandle.ValueOrDefault);
        if (hwnd != IntPtr.Zero && _activated.Add(hwnd)) ActivateAccessibility(hwnd);

        var buttons = new List<string>();
        CollectButtonNames(w, buttons, 0, 25, new[] { 0 }, 400, Stopwatch.StartNew(), 800);
        bool hit = buttons.Any(b =>
        {
            var lower = b.ToLowerInvariant();
            return CallButtonKeywords.Any(kw => lower.Contains(kw));
        });

        // Zur Ferndiagnose die Button-Beschriftungen (UI-Labels, kein Gesprächsinhalt) einmal
        // pro Fenster ins Debug-Log schreiben.
        if (hwnd != IntPtr.Zero && _loggedCallCandidates.Add(hwnd))
            Logger.Log($"Kleines Teams-Fenster {rect.Width}×{rect.Height}, Buttons: [{string.Join(" | ", buttons.Take(8))}]{(hit ? " -> ANRUF erkannt" : "")}");
        if (_loggedCallCandidates.Count > 200) _loggedCallCandidates.Clear();

        return hit;
    }

    private void SetCallVisible(bool visible)
    {
        if (visible == _lastCallVisible) return;
        _lastCallVisible = visible;
        Logger.Log(visible ? "Anruf-Popup sichtbar (eingehender Anruf)" : "Anruf-Popup nicht mehr sichtbar");
        IncomingCallVisibleChanged?.Invoke(this, visible);
    }

    private static void CollectButtonNames(AutomationElement el, List<string> outList,
        int depth, int maxDepth, int[] count, int maxCount, Stopwatch sw, int maxMs)
    {
        if (depth > maxDepth || count[0] >= maxCount || sw.ElapsedMilliseconds > maxMs) return;
        count[0]++;
        if (Safe(() => el.Properties.ControlType.ValueOrDefault) == ControlType.Button)
        {
            var name = Safe(() => el.Properties.Name.ValueOrDefault) ?? "";
            if (!string.IsNullOrWhiteSpace(name)) outList.Add(name.Trim());
        }
        AutomationElement[] children;
        try { children = el.FindAllChildren(); }
        catch { return; }
        foreach (var c in children)
            CollectButtonNames(c, outList, depth + 1, maxDepth, count, maxCount, sw, maxMs);
    }

    // --- Caption-Gruppen einsammeln --------------------------------------
    private static void CollectCaptionGroups(AutomationElement el, List<AutomationElement> outList,
        int depth, int maxDepth, int[] count, int maxCount, Stopwatch sw, int maxMs)
    {
        if (depth > maxDepth || count[0] >= maxCount || sw.ElapsedMilliseconds > maxMs) return;
        count[0]++;

        if (Safe(() => el.Properties.ControlType.ValueOrDefault) == ControlType.Group)
        {
            string cls = Safe(() => el.Properties.ClassName.ValueOrDefault) ?? "";
            if (cls.Contains("ChatMessageCompact", StringComparison.Ordinal) ||
                cls.Contains("closed-caption-chat-message", StringComparison.Ordinal))
            {
                outList.Add(el);
                return; // Kinder sind die Text-Knoten – nicht tiefer suchen
            }
        }

        AutomationElement[] children;
        try { children = el.FindAllChildren(); }
        catch { return; }
        foreach (var c in children)
            CollectCaptionGroups(c, outList, depth + 1, maxDepth, count, maxCount, sw, maxMs);
    }

    private static CaptionLine GetLineFromGroup(AutomationElement group)
    {
        var texts = new List<string>();
        CollectTexts(group, texts, 0, 4, new[] { 0 }, 40);
        // Struktur: [0] = Sprechername, [1..] = gesprochener Text.
        // Ist nur EIN Knoten da (Übergangszustand beim Erscheinen), ist das der Sprechername –
        // NICHT als gesprochener Text werten (sonst würde der eigene Name als "Text" fehl-triggern).
        if (texts.Count <= 1) return new CaptionLine(texts.Count == 1 ? texts[0] : "", "");
        return new CaptionLine(texts[0], string.Join(" ", texts.Skip(1)));
    }

    private static void CollectTexts(AutomationElement el, List<string> outList,
        int depth, int maxDepth, int[] count, int maxCount)
    {
        if (depth > maxDepth || count[0] >= maxCount) return;
        count[0]++;
        if (Safe(() => el.Properties.ControlType.ValueOrDefault) == ControlType.Text)
        {
            var name = Safe(() => el.Properties.Name.ValueOrDefault) ?? "";
            if (!string.IsNullOrWhiteSpace(name)) outList.Add(name.Trim());
        }
        AutomationElement[] children;
        try { children = el.FindAllChildren(); }
        catch { return; }
        foreach (var c in children)
            CollectTexts(c, outList, depth + 1, maxDepth, count, maxCount);
    }

    private void SetAvailable(bool available, string message)
    {
        bool changed = available != _lastAvailable;
        _lastAvailable = available;
        if (changed || available) // im Verfügbar-Zustand Zeilenzahl aktuell halten
            StatusChanged?.Invoke(this, new TranscriptStatusEventArgs(available, message));
    }

    private static T? Safe<T>(Func<T> f)
    {
        try { return f(); } catch { return default; }
    }

    // --- Chromium-Accessibility aktivieren (wie in Phase 0 verifiziert) ---
    private const uint WM_GETOBJECT = 0x003D;
    private const uint SMTO_ABORTIFHUNG = 0x0002;
    private const uint OBJID_CLIENT = 0xFFFFFFFC;
    private static Guid IID_IAccessible = new("618736E0-3C3D-11CF-810C-00AA00389B71");

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
        uint flags, uint timeoutMs, out IntPtr result);

    [DllImport("oleacc.dll")]
    private static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint id, ref Guid iid,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppvObject);

    private static void ActivateAccessibility(IntPtr topHwnd)
    {
        if (topHwnd == IntPtr.Zero) return;
        var targets = new List<IntPtr>();
        EnumWindowsProc cb = (h, _) =>
        {
            var sb = new StringBuilder(256);
            GetClassName(h, sb, sb.Capacity);
            var cls = sb.ToString();
            if (cls.Contains("Chrome_RenderWidgetHostHWND") || cls.Contains("RenderWidget"))
                targets.Add(h);
            return true;
        };
        EnumChildWindows(topHwnd, cb, IntPtr.Zero);
        GC.KeepAlive(cb);
        targets.Add(topHwnd);

        foreach (var h in targets)
        {
            SendMessageTimeout(h, WM_GETOBJECT, IntPtr.Zero, (IntPtr)1, SMTO_ABORTIFHUNG, 600, out _);
            SendMessageTimeout(h, WM_GETOBJECT, IntPtr.Zero, (IntPtr)unchecked((int)OBJID_CLIENT),
                SMTO_ABORTIFHUNG, 600, out _);
            try
            {
                if (AccessibleObjectFromWindow(h, OBJID_CLIENT, ref IID_IAccessible, out var acc) == 0 && acc != null)
                {
                    try { _ = ((dynamic)acc).accName[0]; }
                    catch { try { _ = ((dynamic)acc).get_accName(0); } catch { } }
                }
            }
            catch { }
        }
    }
}
