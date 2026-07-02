using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using NAudio.CoreAudioApi;
using Windows.Media.Control;

namespace TeamsMentionNotificationCenter.Diagnostics;

/// <summary>
/// Phase-0 Machbarkeits-Diagnose für TeamsMentionNotificationCenter.
/// Beweist auf dem Zielrechner:
///  (1) Teams-Live-Untertitel lassen sich per UI Automation auslesen (Struktur-Dump + Live-Watch),
///  (2) Spotify lässt sich per WinRT-SMTC pausieren/fortsetzen,
///  (3) die Teams-Audio-Session lässt sich per NAudio in der Lautstärke/Mute steuern.
///
/// Datenschutz: Test 1 kennt einen "Struktur-Modus" (Standard), der NUR Element-Typ/-Struktur
/// und Textlänge protokolliert – NICHT den gesprochenen Wortlaut. So lässt sich das Auslesen
/// auch in einem echten Meeting verifizieren, ohne fremde Gesprächsinhalte preiszugeben.
/// </summary>
internal static class Program
{
    private static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("======================================================");
        Console.WriteLine(" TeamsMentionNotificationCenter – Diagnose (Phase 0)");
        Console.WriteLine("======================================================");
        Console.WriteLine("Tipp für Test 1: In Teams einen Call starten, Untertitel");
        Console.WriteLine("aktivieren (More actions -> Language and speech -> Show live");
        Console.WriteLine("captions) und – wenn möglich – 'Open captions in new window'.");

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("Auswahl:");
            Console.WriteLine("  [1] Teams-Untertitel via UI Automation entdecken (Struktur-Dump + Live-Watch)");
            Console.WriteLine("  [2] Medien-Sessions (SMTC) auflisten + Spotify Play/Pause testen");
            Console.WriteLine("  [3] Teams-Audio-Session finden + Lautstärke/Mute testen");
            Console.WriteLine("  [4] Alle Teams-Fenster auflisten (Titel / HWND / Klasse)");
            Console.WriteLine("  [q] Beenden");
            Console.Write("> ");
            var choice = Console.ReadLine()?.Trim().ToLowerInvariant();
            try
            {
                switch (choice)
                {
                    case "1": RunUiaProbe(); break;
                    case "2": await RunSmtcProbe(); break;
                    case "3": RunAudioProbe(); break;
                    case "4": ListTeamsWindows(); break;
                    case "q": case "quit": case "exit": return;
                    default: Console.WriteLine("Unbekannte Auswahl."); break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FEHLER: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }

    private readonly record struct TextNode(string Text, string ControlType, string Id, string Class, bool Offscreen);

    // ---------------------------------------------------------------------
    // (1) UI Automation – Teams-Untertitel
    // ---------------------------------------------------------------------
    private static void RunUiaProbe()
    {
        var teamsPids = GetTeamsPids();
        if (teamsPids.Count == 0)
        {
            Console.WriteLine("Kein 'ms-teams'-Prozess gefunden. Läuft das neue Teams?");
            return;
        }

        using var automation = new UIA3Automation();
        var desktop = automation.GetDesktop();
        var windows = desktop.FindAllChildren()
            .Where(e => teamsPids.Contains(Safe(() => e.Properties.ProcessId.ValueOrDefault)))
            .ToList();

        if (windows.Count == 0)
        {
            Console.WriteLine("Teams läuft, aber es wurde kein Top-Level-Fenster gefunden.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Gefundene Teams-Fenster:");
        for (int i = 0; i < windows.Count; i++)
        {
            var w = windows[i];
            string name = Safe(() => w.Properties.Name.ValueOrDefault) ?? "";
            string cls = Safe(() => w.Properties.ClassName.ValueOrDefault) ?? "";
            var hwnd = Safe(() => w.Properties.NativeWindowHandle.ValueOrDefault);
            string hint = LooksLikeCaptionWindow(name) ? "  <-- evtl. Untertitel-Fenster" : "";
            Console.WriteLine($"  [{i}] '{Trunc(name, 60)}'  class='{cls}'  hwnd=0x{hwnd.ToInt64():X}{hint}");
        }

        Console.Write("Fenster-Index für die Analyse (Enter = automatisch Untertitel-Fenster, sonst 0): ");
        var idxStr = Console.ReadLine()?.Trim();
        int idx;
        if (string.IsNullOrEmpty(idxStr))
        {
            idx = windows.FindIndex(w => LooksLikeCaptionWindow(Safe(() => w.Properties.Name.ValueOrDefault) ?? ""));
            if (idx < 0) idx = 0;
        }
        else if (!int.TryParse(idxStr, out idx)) idx = 0;
        if (idx < 0 || idx >= windows.Count) idx = 0;
        Console.WriteLine($"Analysiere Fenster [{idx}]: '{Trunc(Safe(() => windows[idx].Properties.Name.ValueOrDefault), 60)}'");
        var target = windows[idx];
        var targetHwnd = Safe(() => target.Properties.NativeWindowHandle.ValueOrDefault);

        Console.WriteLine();
        Console.WriteLine("Modus:  [s] = nur Struktur (empfohlen in echten Meetings, KEIN Wortlaut)");
        Console.WriteLine("        [w] = mit Wortlaut (nur in Testmeetings verwenden)");
        Console.Write("Wahl (Enter = s): ");
        bool redact = (Console.ReadLine()?.Trim().ToLowerInvariant()) != "w";
        Console.WriteLine(redact
            ? ">> Struktur-Modus: es wird KEIN gesprochener Text ausgegeben/gespeichert."
            : ">> Wortlaut-Modus: gesprochener Text wird angezeigt und in den Dump geschrieben.");

        // Chromium/WebView2 baut seinen Accessibility-Baum erst, wenn ein AT-Client zugreift.
        // Robuste Aktivierung: WM_GETOBJECT (custom id 1 + OBJID_CLIENT) + MSAA accName(0) auf die
        // Render-Fenster, danach warten, bis der Baum tatsächlich (asynchron) aufgebaut ist.
        Console.WriteLine("Aktiviere Chromium-Accessibility ...");
        var renders = FindRenderWidgets(targetHwnd);
        Console.WriteLine($"  Render-Fenster (Chrome_RenderWidgetHostHWND): {renders.Count}");
        ActivateAccessibility(targetHwnd, renders);

        var poll = Stopwatch.StartNew();
        int lastCount = 0;
        while (poll.ElapsedMilliseconds < 12000)
        {
            Thread.Sleep(600);
            var fresh = Safe(() => automation.FromHandle(targetHwnd));
            int c = fresh != null ? CountDescendants(fresh, 60, 4000, 4000) : 0;
            Console.WriteLine($"  [{poll.ElapsedMilliseconds,5} ms] Knoten im Baum: {c}");
            if (c > 40) break;                                   // Web-Inhalt ist da
            if (c > 0 && c == lastCount && poll.ElapsedMilliseconds > 3500) break; // stabil, aber leer
            lastCount = c;
            ActivateAccessibility(targetHwnd, renders);          // erneut anstupsen
        }
        target = Safe(() => automation.FromHandle(targetHwnd)) ?? target; // frisch für die Analyse

        // UIA-Baum-Dump schreiben (im Struktur-Modus ohne Wortlaut – nur Typ/Struktur/Länge).
        string dumpPath = Path.Combine(Directory.GetCurrentDirectory(),
            $"uia-dump-{(redact ? "struct" : "text")}-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        Console.WriteLine($"Schreibe UIA-Baum-Dump -> {dumpPath}");
        int nodeCount = 0;
        using (var sw = new StreamWriter(dumpPath, false, Encoding.UTF8))
        {
            string winName = redact ? "(ausgeblendet)" : Safe(() => target.Properties.Name.ValueOrDefault) ?? "";
            sw.WriteLine($"UIA-Dump  Modus={(redact ? "STRUKTUR (kein Wortlaut)" : "WORTLAUT")}  Fenster='{winName}'  {DateTime.Now:O}");
            sw.WriteLine("Format: <ControlType> name=.. id='..' class='..' [offscreen]");
            sw.WriteLine(new string('-', 70));
            var sw2 = Stopwatch.StartNew();
            DumpElement(target, sw, 0, maxDepth: 60, ref nodeCount, maxCount: 8000, sw2, maxMs: 20000, redact);
        }
        Console.WriteLine($"Dump fertig ({nodeCount} Knoten).");

        // Momentaufnahme der Textknoten – zeigt, ob/wie Caption-Text erreichbar ist.
        var nodes = new List<TextNode>();
        CollectTextNodes(target, nodes, 0, 60, new[] { 0 }, 8000, Stopwatch.StartNew(), 15000);
        var withText = nodes.Where(n => n.Text.Length is > 1 and < 400).ToList();

        Console.WriteLine();
        Console.WriteLine($"Textknoten gefunden: {withText.Count}");
        if (redact)
        {
            Console.WriteLine("Struktur der Textknoten (Typ / class / id / Textlänge) – ohne Wortlaut:");
            foreach (var g in withText
                         .GroupBy(n => (n.ControlType, n.Class, n.Id))
                         .OrderByDescending(g => g.Count())
                         .Take(30))
            {
                var (ct, cls, id) = g.Key;
                var lens = g.Select(n => n.Text.Length).OrderBy(x => x).ToList();
                Console.WriteLine($"   {g.Count(),3}x  <{ct}>  class='{Trunc(cls, 40)}'  id='{Trunc(id, 30)}'  " +
                                  $"len[min={lens.First()},max={lens.Last()}]");
            }
        }
        else
        {
            Console.WriteLine("Sichtbare Texte (Auszug):");
            foreach (var t in withText.Select(n => n.Text).Distinct().Take(40))
                Console.WriteLine($"   • {Trunc(t, 100)}");
        }

        Console.WriteLine();
        Console.Write("Live-Watch starten? (erkennt neue Zeilen fortlaufend) [j/N]: ");
        if ((Console.ReadLine()?.Trim().ToLowerInvariant()) is "j" or "y" or "ja")
            LiveWatch(target, redact);
    }

    private static void LiveWatch(AutomationElement root, bool redact)
    {
        Console.WriteLine();
        Console.WriteLine("=== LIVE-WATCH === Sprich im Call / lass deinen Namen fallen.");
        Console.WriteLine(redact
            ? "(Struktur-Modus: es wird nur 'neue Zeile erkannt' + Typ/Länge gemeldet, kein Wortlaut.)"
            : "(Wortlaut-Modus: neue Texte werden im Klartext angezeigt.)");
        Console.WriteLine("(Beenden mit beliebiger Taste.)");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var initial = new List<TextNode>();
        CollectTextNodes(root, initial, 0, 60, new[] { 0 }, 8000, Stopwatch.StartNew(), 10000);
        foreach (var n in initial) seen.Add(n.Text);

        while (!Console.KeyAvailable)
        {
            try
            {
                var batch = new List<TextNode>();
                CollectTextNodes(root, batch, 0, 60, new[] { 0 }, 8000, Stopwatch.StartNew(), 8000);
                foreach (var n in batch)
                {
                    if (n.Text.Length is <= 1 or > 400) continue;
                    if (!seen.Add(n.Text)) continue;
                    Console.WriteLine(redact
                        ? $"[{DateTime.Now:HH:mm:ss}] + neue Zeile  <{n.ControlType}> class='{Trunc(n.Class, 30)}' len={n.Text.Length}"
                        : $"[{DateTime.Now:HH:mm:ss}] {n.Text}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"(watch-Fehler: {ex.Message})");
            }
            Thread.Sleep(700);
        }
        Console.ReadKey(true);
        Console.WriteLine("Live-Watch beendet.");
    }

    private static void DumpElement(AutomationElement el, StreamWriter w, int depth, int maxDepth,
        ref int count, int maxCount, Stopwatch sw, int maxMs, bool redact)
    {
        if (depth > maxDepth || count >= maxCount || sw.ElapsedMilliseconds > maxMs) return;
        count++;
        string indent = new(' ', depth * 2);
        string ct = Safe(() => el.Properties.ControlType.ValueOrDefault.ToString()) ?? "?";
        string name = Safe(() => el.Properties.Name.ValueOrDefault) ?? "";
        string aid = Safe(() => el.Properties.AutomationId.ValueOrDefault) ?? "";
        string cls = Safe(() => el.Properties.ClassName.ValueOrDefault) ?? "";
        bool off = Safe(() => el.Properties.IsOffscreen.ValueOrDefault);
        string nameField = redact
            ? (name.Length > 0 ? $"len={name.Length}" : "-")
            : $"'{Trunc(name, 120)}'";
        w.WriteLine($"{indent}<{ct}> name={nameField} id='{aid}' class='{cls}'{(off ? " [offscreen]" : "")}");

        AutomationElement[] children;
        try { children = el.FindAllChildren(); }
        catch { return; }
        foreach (var c in children)
            DumpElement(c, w, depth + 1, maxDepth, ref count, maxCount, sw, maxMs, redact);
    }

    private static void CollectTextNodes(AutomationElement el, List<TextNode> outList, int depth, int maxDepth,
        int[] count, int maxCount, Stopwatch sw, int maxMs)
    {
        if (depth > maxDepth || count[0] >= maxCount || sw.ElapsedMilliseconds > maxMs) return;
        count[0]++;
        string name = Safe(() => el.Properties.Name.ValueOrDefault) ?? "";
        if (!string.IsNullOrWhiteSpace(name))
        {
            string ct = Safe(() => el.Properties.ControlType.ValueOrDefault.ToString()) ?? "?";
            string aid = Safe(() => el.Properties.AutomationId.ValueOrDefault) ?? "";
            string cls = Safe(() => el.Properties.ClassName.ValueOrDefault) ?? "";
            bool off = Safe(() => el.Properties.IsOffscreen.ValueOrDefault);
            outList.Add(new TextNode(name.Trim(), ct, aid, cls, off));
        }

        AutomationElement[] children;
        try { children = el.FindAllChildren(); }
        catch { return; }
        foreach (var c in children)
            CollectTextNodes(c, outList, depth + 1, maxDepth, count, maxCount, sw, maxMs);
    }

    private static int CountDescendants(AutomationElement root, int maxDepth, int maxCount, int maxMs)
    {
        int n = 0;
        var sw = Stopwatch.StartNew();
        void Rec(AutomationElement e, int d)
        {
            if (d > maxDepth || n >= maxCount || sw.ElapsedMilliseconds > maxMs) return;
            n++;
            AutomationElement[] ch;
            try { ch = e.FindAllChildren(); } catch { return; }
            foreach (var c in ch) Rec(c, d + 1);
        }
        Rec(root, 0);
        return n;
    }

    private static bool LooksLikeCaptionWindow(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        name = name.ToLowerInvariant();
        return name.Contains("caption") || name.Contains("untertitel") || name.Contains("beschriftung") ||
               name.Contains("transcript") || name.Contains("transkript") || name.Contains("live captions");
    }

    // ---------------------------------------------------------------------
    // (2) SMTC – Medien-/Spotify-Steuerung
    // ---------------------------------------------------------------------
    private static async Task RunSmtcProbe()
    {
        var mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        var sessions = mgr.GetSessions();
        if (sessions.Count == 0)
        {
            Console.WriteLine("Keine Medien-Sessions gefunden. Läuft Spotify (oder eine andere Wiedergabe)?");
            return;
        }

        GlobalSystemMediaTransportControlsSession? spotify = null;
        Console.WriteLine();
        Console.WriteLine("Medien-Sessions:");
        int i = 0;
        foreach (var s in sessions)
        {
            string appId = s.SourceAppUserModelId ?? "";
            var pb = s.GetPlaybackInfo();
            string title = "", artist = "";
            try
            {
                var mp = await s.TryGetMediaPropertiesAsync();
                title = mp.Title ?? "";
                artist = mp.Artist ?? "";
            }
            catch { /* manche Sessions liefern keine Properties */ }
            Console.WriteLine($"  [{i}] {appId}  | {pb.PlaybackStatus} | {Trunc($"{artist} – {title}", 60)}");
            if (appId.Contains("Spotify", StringComparison.OrdinalIgnoreCase)) spotify = s;
            i++;
        }

        var target = spotify ?? mgr.GetCurrentSession();
        if (target == null) { Console.WriteLine("Kein Ziel bestimmbar."); return; }
        Console.WriteLine($"Ziel-Session: {target.SourceAppUserModelId} " +
                          $"(Status {target.GetPlaybackInfo().PlaybackStatus})" +
                          (spotify != null ? "  [Spotify erkannt]" : "  [aktive Session]"));
        Console.Write("Test:  [p]=Pause  [r]=Play/Resume  Enter=nichts: ");
        var k = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (k == "p") Console.WriteLine("TryPauseAsync -> " + await target.TryPauseAsync());
        else if (k == "r") Console.WriteLine("TryPlayAsync  -> " + await target.TryPlayAsync());
    }

    // ---------------------------------------------------------------------
    // (3) NAudio – Teams-Audio-Session
    // ---------------------------------------------------------------------
    private static void RunAudioProbe()
    {
        var teamsPids = GetTeamsPids();
        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var sm = device.AudioSessionManager;
        var sessions = sm.Sessions;

        AudioSessionControl? teams = null;
        Console.WriteLine();
        Console.WriteLine($"Audio-Sessions am Standard-Ausgabegerät ('{device.FriendlyName}'):");
        for (int i = 0; i < sessions.Count; i++)
        {
            var s = sessions[i];
            uint pid = s.GetProcessID;
            string pname = "";
            try { pname = Process.GetProcessById((int)pid).ProcessName; } catch { }
            float vol = s.SimpleAudioVolume.Volume;
            bool mute = s.SimpleAudioVolume.Mute;
            Console.WriteLine($"  pid={pid,-6} '{pname}'  vol={vol:P0}  mute={mute}  disp='{Trunc(s.DisplayName, 30)}'");
            if (teamsPids.Contains((int)pid)) teams = s;
        }

        if (teams == null)
        {
            Console.WriteLine("Keine 'ms-teams'-Audio-Session gefunden. Spielt Teams gerade Ton ab?");
            Console.WriteLine("(Eine App taucht erst als Session auf, wenn sie tatsächlich Audio ausgibt.)");
            return;
        }

        Console.WriteLine($"Teams-Session gefunden. Aktuell: vol={teams.SimpleAudioVolume.Volume:P0} mute={teams.SimpleAudioVolume.Mute}");
        Console.Write("Test:  [1]=15%  [2]=100%  [m]=Mute umschalten  Enter=nichts: ");
        var k = Console.ReadLine()?.Trim().ToLowerInvariant();
        switch (k)
        {
            case "1": teams.SimpleAudioVolume.Volume = 0.15f; break;
            case "2": teams.SimpleAudioVolume.Volume = 1.0f; break;
            case "m": teams.SimpleAudioVolume.Mute = !teams.SimpleAudioVolume.Mute; break;
        }
        Console.WriteLine($"Neu: vol={teams.SimpleAudioVolume.Volume:P0} mute={teams.SimpleAudioVolume.Mute}");
    }

    // ---------------------------------------------------------------------
    // (4) Teams-Fenster auflisten
    // ---------------------------------------------------------------------
    private static void ListTeamsWindows()
    {
        var teamsPids = GetTeamsPids();
        if (teamsPids.Count == 0) { Console.WriteLine("Kein 'ms-teams'-Prozess gefunden."); return; }
        using var automation = new UIA3Automation();
        var windows = automation.GetDesktop().FindAllChildren()
            .Where(e => teamsPids.Contains(Safe(() => e.Properties.ProcessId.ValueOrDefault)))
            .ToList();
        Console.WriteLine($"{windows.Count} Teams-Top-Level-Fenster:");
        foreach (var w in windows)
        {
            string name = Safe(() => w.Properties.Name.ValueOrDefault) ?? "";
            string cls = Safe(() => w.Properties.ClassName.ValueOrDefault) ?? "";
            var hwnd = Safe(() => w.Properties.NativeWindowHandle.ValueOrDefault);
            string hint = LooksLikeCaptionWindow(name) ? "  <-- evtl. Untertitel-Fenster" : "";
            Console.WriteLine($"  '{Trunc(name, 70)}'  class='{cls}'  hwnd=0x{hwnd.ToInt64():X}{hint}");
        }
    }

    // ---------------------------------------------------------------------
    // Helfer
    // ---------------------------------------------------------------------
    private static HashSet<int> GetTeamsPids() =>
        Process.GetProcessesByName("ms-teams").Select(p => p.Id).ToHashSet();

    private static T? Safe<T>(Func<T> f)
    {
        try { return f(); } catch { return default; }
    }

    private static string Trunc(string? s, int max)
    {
        s ??= "";
        s = s.Replace("\r", " ").Replace("\n", " ");
        return s.Length <= max ? s : s[..max] + "…";
    }

    // --- Win32/MSAA: Chromium-Accessibility aktivieren ---
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

    private static List<IntPtr> FindRenderWidgets(IntPtr top)
    {
        var list = new List<IntPtr>();
        if (top == IntPtr.Zero) return list;
        EnumWindowsProc cb = (h, _) =>
        {
            var sb = new StringBuilder(256);
            GetClassName(h, sb, sb.Capacity);
            string cls = sb.ToString();
            if (cls.Contains("Chrome_RenderWidgetHostHWND") || cls.Contains("RenderWidget"))
                list.Add(h);
            return true;
        };
        EnumChildWindows(top, cb, IntPtr.Zero);
        GC.KeepAlive(cb);
        return list;
    }

    /// <summary>
    /// Zwingt Chromium/WebView2, seinen Accessibility-Baum aufzubauen:
    /// WM_GETOBJECT (custom id 1 = Chrome-AT-Detection, und OBJID_CLIENT) plus
    /// MSAA AccessibleObjectFromWindow + accName(0) auf jedes Render-Fenster.
    /// </summary>
    private static void ActivateAccessibility(IntPtr top, List<IntPtr> renders)
    {
        var targets = new List<IntPtr>(renders);
        if (top != IntPtr.Zero) targets.Add(top);
        foreach (var h in targets)
        {
            SendMessageTimeout(h, WM_GETOBJECT, IntPtr.Zero, (IntPtr)1, SMTO_ABORTIFHUNG, 800, out _);
            SendMessageTimeout(h, WM_GETOBJECT, IntPtr.Zero, (IntPtr)unchecked((int)OBJID_CLIENT),
                SMTO_ABORTIFHUNG, 800, out _);
            try
            {
                if (AccessibleObjectFromWindow(h, OBJID_CLIENT, ref IID_IAccessible, out var acc) == 0 && acc != null)
                {
                    try { _ = ((dynamic)acc).accName[0]; }
                    catch { try { _ = ((dynamic)acc).get_accName(0); } catch { } }
                }
            }
            catch { /* MSAA nicht verfügbar – WM_GETOBJECT muss reichen */ }
        }
    }
}
