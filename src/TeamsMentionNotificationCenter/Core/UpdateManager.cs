using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using TeamsMentionNotificationCenter.Localization;

namespace TeamsMentionNotificationCenter.Core;

/// <summary>
/// Prüft die GitHub-Releases des Projekts auf eine neuere Version und aktualisiert die App selbst.
///
/// Selbst-Update einer Single-File-EXE ohne separaten Updater: Die neue EXE wird neben die laufende
/// heruntergeladen, die laufende EXE wird in ".old" UMBENANNT (bei laufendem Prozess erlaubt – nur
/// Überschreiben ist gesperrt), die neue rückt an ihren Platz, dann Neustart. Beim nächsten Start
/// entfernt <see cref="CleanupAfterUpdate"/> die Reste. Funktioniert überall dort, wo der Nutzer
/// Schreibrechte auf den Programmordner hat (z. B. %LOCALAPPDATA%\Programs, Downloads) – bewusst
/// KEIN C:\Program Files, das würde für jedes Update Adminrechte erzwingen.
/// </summary>
public static class UpdateManager
{
    private const string RepoApi = "https://api.github.com/repos/robertkieschke/TeamsMentionNotificationCenter";
    private const string ApiLatest = RepoApi + "/releases/latest";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient();
        c.DefaultRequestHeaders.UserAgent.ParseAdd("TeamsMentionNotificationCenter-Updater"); // von GitHub gefordert
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        c.Timeout = TimeSpan.FromMinutes(10); // Download der ~80-MB-EXE
        return c;
    }

    public sealed record ReleaseInfo(Version Version, string Tag, string AssetUrl);

    public static Version CurrentVersion =>
        Version.TryParse(AppInfo.Version, out var v) ? Normalize(v) : new Version(0, 0, 0);

    private static Version Normalize(Version v) =>
        new(v.Major, Math.Max(v.Minor, 0), Math.Max(v.Build, 0));

    /// <summary>
    /// Fragt das neueste Release ab. Liefert null, wenn keine neuere Version existiert.
    /// Wirft bei Netzwerk-/Zugriffsfehlern (z. B. offline oder Repository nicht öffentlich erreichbar).
    /// </summary>
    public static async Task<ReleaseInfo?> CheckAsync()
    {
        using var resp = await Http.GetAsync(ApiLatest);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        string tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var remote)) return null;
        if (Normalize(remote) <= CurrentVersion) return null;

        string? url = null;
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var a in assets.EnumerateArray())
            {
                var name = a.GetProperty("name").GetString() ?? "";
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    url = a.GetProperty("browser_download_url").GetString();
                    break;
                }
            }
        }
        return url == null ? null : new ReleaseInfo(Normalize(remote), tag, url);
    }

    /// <summary>Holt Tag, Notes-Text (Markdown) und Release-URL zu einer konkreten Version
    /// (z. B. nach einem Update für die „Was ist neu"-Anzeige). Liefert null, wenn kein Release existiert.</summary>
    public static async Task<(string Tag, string Body, string Url)?> GetReleaseNotesAsync(string version)
    {
        using var resp = await Http.GetAsync($"{RepoApi}/releases/tags/v{version}");
        if (!resp.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        return (root.GetProperty("tag_name").GetString() ?? ("v" + version),
                root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "",
                root.TryGetProperty("html_url", out var u) ? u.GetString() ?? "" : "");
    }

    /// <summary>
    /// Extrahiert aus mehrsprachigen Release-Notes den Abschnitt der gewünschten Sprache.
    /// Erwartetes Autoren-Format: Abschnitte mit den Überschriften „## Deutsch", „## English",
    /// „## Italiano" (Ebene egal). Ohne solche Überschriften – oder wenn der gewünschte Abschnitt
    /// fehlt/leer ist – wird der komplette Text zurückgegeben (kompatibel zu alten Releases).
    /// </summary>
    public static string ExtractLanguageSection(string body, AppLanguage language)
    {
        string wanted = language switch { AppLanguage.En => "english", AppLanguage.It => "italiano", _ => "deutsch" };
        string[] known = { "deutsch", "english", "italiano" };

        var sb = new System.Text.StringBuilder();
        bool inWanted = false, anyHeading = false;
        foreach (var line in (body ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("#"))
            {
                var title = trimmed.TrimStart('#').Trim().TrimEnd(':').ToLowerInvariant();
                if (known.Contains(title))
                {
                    anyHeading = true;
                    inWanted = title == wanted;
                    continue; // Sprachüberschrift selbst nicht mit anzeigen
                }
            }
            if (inWanted) sb.AppendLine(line);
        }

        var section = sb.ToString().Trim();
        return anyHeading && section.Length > 0 ? section : (body ?? "");
    }

    /// <summary>Bereitet Release-Notes für die Textanzeige auf: Sprachabschnitt wählen + Markdown entschärfen.</summary>
    public static string FormatNotesForDisplay(string body, AppLanguage language) =>
        CleanMarkdown(ExtractLanguageSection(body, language));

    public sealed record ReleaseNotesEntry(string Tag, string Body, string Url, DateTime? PublishedAt);

    /// <summary>Alle veröffentlichten Releases (neueste zuerst, max. 20) – für den Release-Notes-Tab.</summary>
    public static async Task<List<ReleaseNotesEntry>> GetAllReleasesAsync()
    {
        using var resp = await Http.GetAsync($"{RepoApi}/releases?per_page=20");
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var list = new List<ReleaseNotesEntry>();
        foreach (var r in doc.RootElement.EnumerateArray())
        {
            if (r.TryGetProperty("draft", out var d) && d.GetBoolean()) continue;
            string tag = r.GetProperty("tag_name").GetString() ?? "";
            string body = r.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            string url = r.TryGetProperty("html_url", out var u) ? u.GetString() ?? "" : "";
            DateTime? published = r.TryGetProperty("published_at", out var p) && p.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(p.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
                ? dt.ToLocalTime() : null;
            list.Add(new ReleaseNotesEntry(tag, body, url, published));
        }
        return list; // GitHub liefert neueste zuerst
    }

    /// <summary>Zeigt die Versionshinweise nach einem Update (nicht-modal), in der UI-Sprache
    /// (sofern die Notes mehrsprachige Abschnitte enthalten).</summary>
    public static void ShowNotesWindow(string tag, string markdownBody, string releaseUrl)
    {
        markdownBody = ExtractLanguageSection(markdownBody, Loc.Language);
        var heading = new TextBlock
        {
            Text = Loc.Tf("Neu in Version {0}", tag.TrimStart('v', 'V')),
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 10)
        };
        var body = new TextBlock { Text = CleanMarkdown(markdownBody), TextWrapping = TextWrapping.Wrap };
        var scroll = new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 340,
            Margin = new Thickness(0, 0, 0, 14)
        };
        Theme.ThinScroll(scroll);
        var openPage = new Button { Content = Loc.T("Release-Seite öffnen"), Padding = new Thickness(14, 5, 14, 5) };
        var close = new Button { Content = Loc.T("Schließen"), Padding = new Thickness(14, 5, 14, 5), Margin = new Thickness(8, 0, 0, 0), IsDefault = true, IsCancel = true };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(openPage);
        buttons.Children.Add(close);
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(heading);
        panel.Children.Add(scroll);
        panel.Children.Add(buttons);

        var win = new Window
        {
            Title = Loc.Tf("Neu in Version {0}", tag.TrimStart('v', 'V')) + " – " + AppInfo.DisplayName,
            Content = panel,
            Width = 560,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true,
            ShowInTaskbar = true,
            Icon = Branding.CreateImageSource(64, Branding.Accent)
        };
        Theme.Prepare(win);
        openPage.Click += (_, _) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(releaseUrl) { UseShellExecute = true }); }
            catch { /* Browser nicht verfügbar */ }
        };
        close.Click += (_, _) => win.Close();
        win.Show();
    }

    /// <summary>Sehr leichte Markdown-Aufbereitung für die Textanzeige (Fett-/Code-Zeichen und
    /// Überschriften-Präfixe entfernen, Listenpunkte hübschen). Kein vollwertiger Renderer nötig.</summary>
    private static string CleanMarkdown(string md)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var raw in (md ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Replace("**", "").Replace("`", "");
            if (line.StartsWith("### ")) line = line[4..];
            else if (line.StartsWith("## ")) line = line[3..];
            else if (line.StartsWith("# ")) line = line[2..];
            if (line.StartsWith("> ")) line = line[2..];
            if (line.TrimStart().StartsWith("- "))
            {
                int indent = line.Length - line.TrimStart().Length;
                line = new string(' ', indent) + "• " + line.TrimStart()[2..];
            }
            sb.AppendLine(line);
        }
        return sb.ToString().Trim();
    }

    /// <summary>Update-Hinweis mit den zwei gewünschten Optionen. Liefert true = jetzt aktualisieren.</summary>
    public static bool AskUser(ReleaseInfo release)
    {
        bool updateNow = false;
        var text = new TextBlock
        {
            Text = Loc.Tf("Eine neue Version {0} ist verfügbar (installiert: {1}). Jetzt aktualisieren? Die Anwendung startet danach automatisch neu.",
                release.Tag, AppInfo.Version),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 14)
        };
        var yes = new Button { Content = Loc.T("Jetzt aktualisieren"), Padding = new Thickness(14, 5, 14, 5), IsDefault = true };
        var later = new Button { Content = Loc.T("Später"), Padding = new Thickness(14, 5, 14, 5), Margin = new Thickness(8, 0, 0, 0), IsCancel = true };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(yes);
        buttons.Children.Add(later);
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(text);
        panel.Children.Add(buttons);

        var win = new Window
        {
            Title = AppInfo.DisplayName,
            Content = panel,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true,
            ShowInTaskbar = true,
            Icon = Branding.CreateImageSource(64, Branding.Accent)
        };
        Theme.Prepare(win);
        yes.Click += (_, _) => { updateNow = true; win.Close(); };
        later.Click += (_, _) => win.Close();
        win.ShowDialog();
        return updateNow;
    }

    /// <summary>
    /// Lädt die neue EXE herunter, tauscht sie gegen die laufende und startet die App neu.
    /// <paramref name="status"/> meldet Fortschrittstexte (z. B. an den Tray);
    /// <paramref name="releaseSingleInstance"/> gibt den Single-Instance-Mutex frei, damit die
    /// neue Instanz nicht am Instanzschutz scheitert.
    /// </summary>
    public static async Task DownloadAndRestartAsync(ReleaseInfo release, Action<string> status, Action releaseSingleInstance,
        string restartArguments = "")
    {
        string exePath = Environment.ProcessPath ?? throw new InvalidOperationException("Eigener Programmpfad unbekannt.");
        string dir = Path.GetDirectoryName(exePath)!;

        // Schreibrechte prüfen, BEVOR 80 MB geladen werden.
        string probe = Path.Combine(dir, ".update-probe");
        try { File.WriteAllText(probe, ""); File.Delete(probe); }
        catch
        {
            throw new InvalidOperationException(
                Loc.T("Der Programmordner ist nicht beschreibbar – verschiebe die App z. B. nach %LOCALAPPDATA%\\Programs, damit sie sich selbst aktualisieren kann."));
        }

        status(Loc.T("Update wird heruntergeladen …"));
        Logger.Log($"Update: lade {release.Tag} von {release.AssetUrl}");
        string tmp = Path.Combine(dir, "TeamsMentionNotificationCenter.update.tmp");
        using (var resp = await Http.GetAsync(release.AssetUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            resp.EnsureSuccessStatusCode();
            await using var src = await resp.Content.ReadAsStreamAsync();
            await using var dst = File.Create(tmp);
            await src.CopyToAsync(dst);
        }

        // Tausch: laufende EXE umbenennen (erlaubt), neue an ihren Platz – mit Rollback bei Fehler.
        string old = exePath + ".old";
        if (File.Exists(old)) File.Delete(old);
        File.Move(exePath, old);
        try { File.Move(tmp, exePath); }
        catch { File.Move(old, exePath); throw; }

        Logger.Log($"Update auf {release.Tag} installiert – Neustart.");
        releaseSingleInstance();
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exePath, restartArguments) { UseShellExecute = true });
        Application.Current.Shutdown();
    }

    /// <summary>Reste eines früheren Updates entfernen (beim App-Start aufrufen).</summary>
    public static void CleanupAfterUpdate()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (exe == null) return;
            string old = exe + ".old";
            if (File.Exists(old)) File.Delete(old);
            string tmp = Path.Combine(Path.GetDirectoryName(exe)!, "TeamsMentionNotificationCenter.update.tmp");
            if (File.Exists(tmp)) File.Delete(tmp);
        }
        catch { /* evtl. noch gesperrt – wird beim nächsten Start entfernt */ }
    }
}
