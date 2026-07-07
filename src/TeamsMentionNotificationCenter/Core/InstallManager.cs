using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using TeamsMentionNotificationCenter.Localization;
using TeamsMentionNotificationCenter.Settings;

namespace TeamsMentionNotificationCenter.Core;

/// <summary>
/// Bietet beim Start an, die (portable) Single-File-EXE nach %LOCALAPPDATA%\Programs zu
/// installieren – dem üblichen Ort für selbst-updatende Benutzer-Apps (VS Code, Discord, …):
/// ohne Adminrechte beschreibbar (Selbst-Update funktioniert garantiert), fester Pfad
/// (Autostart bleibt gültig, Download-Ordner darf aufgeräumt werden) und Startmenü-Eintrag.
/// </summary>
public static class InstallManager
{
    public static string TargetDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs", "TeamsMentionNotificationCenter");

    public static string TargetExe => Path.Combine(TargetDir, "TeamsMentionNotificationCenter.exe");

    /// <summary>Nur für veröffentlichte Single-File-Builds anbieten (bei Framework-Builds aus
    /// bin/ wäre die kopierte EXE ohne ihre DLLs funktionsunfähig; Location ist dort nicht leer).</summary>
    private static bool IsSingleFileBuild =>
        string.IsNullOrEmpty(Assembly.GetEntryAssembly()?.Location) && Environment.ProcessPath != null;

    private static bool IsInstalled =>
        string.Equals(Path.GetDirectoryName(Environment.ProcessPath), TargetDir, StringComparison.OrdinalIgnoreCase);

    public static bool ShouldOffer(AppSettings settings) =>
        settings.OfferInstallOnStartup && IsSingleFileBuild && !IsInstalled;

    /// <summary>Zeigt den Installations-Dialog. Liefert true, wenn installiert und die neue
    /// Instanz gestartet wurde (Aufrufer beendet dann diese Instanz).</summary>
    public static bool OfferAndInstall(AppSettings settings, Action releaseSingleInstance)
    {
        var text = new TextBlock
        {
            Text = Loc.Tf("Soll die App nach {0} installiert werden? Von dort aktualisiert sie sich zuverlässig selbst, erhält einen Startmenü-Eintrag und funktioniert unabhängig von der heruntergeladenen Datei. Sie startet danach automatisch aus dem neuen Ordner.", TargetDir),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };
        var dontAsk = new CheckBox { Content = Loc.T("Nicht mehr fragen"), Margin = new Thickness(0, 0, 0, 12) };
        var install = new Button { Content = Loc.T("Jetzt installieren"), Padding = new Thickness(14, 5, 14, 5), IsDefault = true };
        var later = new Button { Content = Loc.T("Später"), Padding = new Thickness(14, 5, 14, 5), Margin = new Thickness(8, 0, 0, 0), IsCancel = true };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(install);
        buttons.Children.Add(later);
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(text);
        panel.Children.Add(dontAsk);
        panel.Children.Add(buttons);

        bool doInstall = false;
        var win = new Window
        {
            Title = AppInfo.DisplayName,
            Content = panel,
            Width = 500,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true,
            ShowInTaskbar = true,
            Icon = Branding.CreateImageSource(64, Branding.Accent)
        };
        Theme.Prepare(win);
        install.Click += (_, _) => { doInstall = true; win.Close(); };
        later.Click += (_, _) => win.Close();
        win.ShowDialog();

        if (!doInstall)
        {
            if (dontAsk.IsChecked == true)
            {
                settings.OfferInstallOnStartup = false;
                settings.Save();
            }
            return false;
        }

        try
        {
            string source = Environment.ProcessPath!;
            Directory.CreateDirectory(TargetDir);
            File.Copy(source, TargetExe, overwrite: true);
            CreateStartMenuShortcut();
            Logger.Log($"Installiert nach {TargetExe} – Neustart von dort.");

            releaseSingleInstance();
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(TargetExe) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            Logger.Log("Installation fehlgeschlagen: " + ex.Message);
            Theme.ShowMessage(Loc.Tf("Installation fehlgeschlagen: {0}", ex.Message), warning: true);
            return false; // portabel weiterlaufen
        }
    }

    private static void CreateStartMenuShortcut()
    {
        try
        {
            var lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                AppInfo.DisplayName + ".lnk");
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;
            dynamic shell = Activator.CreateInstance(shellType)!;
            var shortcut = shell.CreateShortcut(lnk);
            shortcut.TargetPath = TargetExe;
            shortcut.WorkingDirectory = TargetDir;
            shortcut.Description = AppInfo.DisplayName;
            shortcut.Save();
        }
        catch { /* Verknüpfung ist optional */ }
    }
}
