using System.Windows;
using TeamsMentionNotificationCenter.Core;
using TeamsMentionNotificationCenter.Settings;

namespace TeamsMentionNotificationCenter;

/// <summary>
/// App-Start ohne Hauptfenster: lädt Einstellungen, startet den Controller und lebt im Tray.
/// </summary>
public partial class App : Application
{
    private AppController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = AppSettings.Load();
        // Erststart: settings.json anlegen, damit sie leicht auffindbar/bearbeitbar ist.
        if (!System.IO.File.Exists(AppSettings.SettingsPath))
            settings.Save();

        Localization.Loc.Language = settings.Language;
        Core.Logger.Enabled = settings.DebugLogging;
        Core.Logger.Log($"App gestartet. Trigger-Wörter={settings.TriggerWords.Count}, DetectionEnabled={settings.DetectionEnabled}");

        _controller = new AppController(settings, Dispatcher);
        _controller.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        base.OnExit(e);
    }
}
