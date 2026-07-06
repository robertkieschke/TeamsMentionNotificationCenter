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
        Core.Logger.Enabled = settings.DebugLogging; // früh setzen, damit auch Install/Update-Schritte geloggt werden

        // Nur eine Instanz zulassen: Der Mutex lebt so lange wie der Prozess; stürzt die App ab,
        // räumt Windows das Kernel-Objekt automatisch weg. Ohne Prefix gilt er pro Benutzersitzung.
        // Ist der Mutex belegt, kurz warten: Beim Selbst-Update startet die neue Instanz, während
        // die alte sich gerade beendet – sie soll dann übernehmen statt sich wegzumelden.
        _singleInstanceMutex = new System.Threading.Mutex(true, "TeamsMentionNotificationCenter_SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            bool acquired = false;
            try { acquired = _singleInstanceMutex.WaitOne(TimeSpan.FromSeconds(4)); }
            catch (System.Threading.AbandonedMutexException) { acquired = true; } // Vorgänger hart beendet -> übernehmen
            if (!acquired)
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
        }

        UpdateManager.CleanupAfterUpdate();

        // Portabel gestartet (z. B. aus Downloads)? Installation nach %LOCALAPPDATA%\Programs anbieten.
        if (InstallManager.ShouldOffer(settings) &&
            InstallManager.OfferAndInstall(settings, ReleaseSingleInstanceMutex))
        {
            Shutdown(); // installierte Instanz wurde gestartet und übernimmt
            return;
        }

        // Erststart: settings.json anlegen, damit sie leicht auffindbar/bearbeitbar ist.
        if (!System.IO.File.Exists(AppSettings.SettingsPath))
            settings.Save();

        Core.Logger.Log($"App gestartet. Trigger-Wörter={settings.TriggerWords.Count}, DetectionEnabled={settings.DetectionEnabled}");

        _controller = new AppController(settings, Dispatcher);
        _controller.Start();
    }

    /// <summary>Gibt den Single-Instance-Mutex frei, damit die vom Selbst-Update gestartete
    /// neue Instanz sofort übernehmen kann.</summary>
    public void ReleaseSingleInstanceMutex()
    {
        if (_singleInstanceMutex == null) return;
        try { _singleInstanceMutex.ReleaseMutex(); } catch { /* nicht im Besitz -> egal */ }
        _singleInstanceMutex.Dispose();
        _singleInstanceMutex = null;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        ReleaseSingleInstanceMutex();
        base.OnExit(e);
    }
}
