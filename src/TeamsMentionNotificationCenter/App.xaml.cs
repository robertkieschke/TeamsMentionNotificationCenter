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
        Core.Theme.Initialize(settings.Theme);       // vor dem ersten Fenster (Install-/Update-Dialoge)

        // Abstürze sichtbar machen: UI-Ausnahmen loggen und abfangen (App läuft weiter),
        // alles andere zumindest ins Log schreiben, bevor der Prozess stirbt.
        DispatcherUnhandledException += (_, ex) =>
        {
            Core.Logger.Log("UNBEHANDELTE UI-AUSNAHME: " + ex.Exception);
            try { Core.Theme.ShowMessage(ex.Exception.Message, warning: true, title: AppInfo.DisplayName + " – Fehler"); }
            catch // wenn ausgerechnet das Rendering defekt ist: native Rückfallebene
            {
                MessageBox.Show(ex.Exception.Message, AppInfo.DisplayName + " – Fehler",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            ex.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            Core.Logger.Log("UNBEHANDELTE AUSNAHME: " + ex.ExceptionObject);

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
                Core.Theme.ShowMessage(
                    Localization.Loc.T("Die Anwendung läuft bereits – du findest sie als Symbol im Infobereich der Taskleiste (Tray). Diese zusätzliche Instanz wird jetzt beendet."));
                Shutdown();
                return;
            }
        }

        UpdateManager.CleanupAfterUpdate();

        // Nach einem (Silent-)Update-Neustart: vorherigen Zustand aus den Argumenten wiederherstellen.
        AppMode? resumeMode = null;
        bool resumeMusicPaused = false;
        string? updatedFrom = null;
        foreach (var arg in e.Args)
        {
            if (arg.StartsWith("--resume-mode=", StringComparison.OrdinalIgnoreCase) &&
                Enum.TryParse<AppMode>(arg["--resume-mode=".Length..], ignoreCase: true, out var m))
                resumeMode = m;
            else if (string.Equals(arg, "--resume-music-paused", StringComparison.OrdinalIgnoreCase))
                resumeMusicPaused = true;
            else if (arg.StartsWith("--updated-from=", StringComparison.OrdinalIgnoreCase))
                updatedFrom = arg["--updated-from=".Length..];
        }
        if (resumeMode != null)
            Core.Logger.Log($"Neustart nach Update – Zustand wird wiederhergestellt (Modus {resumeMode}, MusikPausiert={resumeMusicPaused}, von Version {updatedFrom ?? "?"}).");

        // Portabel gestartet (z. B. aus Downloads)? Installation nach %LOCALAPPDATA%\Programs anbieten.
        // (Nicht beim automatischen Neustart nach einem Update – der soll lautlos bleiben.)
        if (resumeMode == null && InstallManager.ShouldOffer(settings) &&
            InstallManager.OfferAndInstall(settings, ReleaseSingleInstanceMutex))
        {
            Shutdown(); // installierte Instanz wurde gestartet und übernimmt
            return;
        }

        // Erststart: settings.json anlegen, damit sie leicht auffindbar/bearbeitbar ist.
        if (!System.IO.File.Exists(AppSettings.SettingsPath))
            settings.Save();

        Core.Logger.Log($"App gestartet. Trigger-Wörter={settings.TriggerWords.Count}, DetectionEnabled={settings.DetectionEnabled}");

        _controller = new AppController(settings, Dispatcher)
        {
            ResumeMode = resumeMode,
            ResumeMusicPausedByUs = resumeMusicPaused,
            UpdatedFromVersion = updatedFrom
        };
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
