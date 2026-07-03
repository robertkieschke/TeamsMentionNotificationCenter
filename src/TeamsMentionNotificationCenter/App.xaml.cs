using System.Windows;
using TeamsMentionNotificationCenter.Core;
using TeamsMentionNotificationCenter.Settings;

namespace TeamsMentionNotificationCenter;

/// <summary>
/// App-Start ohne Hauptfenster: lädt Einstellungen, startet den Controller und lebt im Tray.
/// Erlaubt nur EINE Instanz pro Benutzersitzung (benannter Mutex).
/// </summary>
public partial class App : Application
{
    private AppController? _controller;
    private System.Threading.Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = AppSettings.Load();
        Localization.Loc.Language = settings.Language;

        // Nur eine Instanz zulassen: Der Mutex lebt so lange wie der Prozess; stürzt die App ab,
        // räumt Windows das Kernel-Objekt automatisch weg. Ohne Prefix gilt er pro Benutzersitzung.
        _singleInstanceMutex = new System.Threading.Mutex(true, "TeamsMentionNotificationCenter_SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            MessageBox.Show(
                Localization.Loc.T("Die Anwendung läuft bereits – du findest sie als Symbol im Infobereich der Taskleiste (Tray). Diese zusätzliche Instanz wird jetzt beendet."),
                AppInfo.DisplayName,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Erststart: settings.json anlegen, damit sie leicht auffindbar/bearbeitbar ist.
        if (!System.IO.File.Exists(AppSettings.SettingsPath))
            settings.Save();

        Core.Logger.Enabled = settings.DebugLogging;
        Core.Logger.Log($"App gestartet. Trigger-Wörter={settings.TriggerWords.Count}, DetectionEnabled={settings.DetectionEnabled}");

        _controller = new AppController(settings, Dispatcher);
        _controller.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        if (_singleInstanceMutex != null)
        {
            try { _singleInstanceMutex.ReleaseMutex(); } catch { /* nicht im Besitz -> egal */ }
            _singleInstanceMutex.Dispose();
        }
        base.OnExit(e);
    }
}
