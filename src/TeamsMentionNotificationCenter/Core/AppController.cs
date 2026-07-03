using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using TeamsMentionNotificationCenter.Audio;
using TeamsMentionNotificationCenter.Detection;
using TeamsMentionNotificationCenter.Input;
using TeamsMentionNotificationCenter.Localization;
using TeamsMentionNotificationCenter.Overlay;
using TeamsMentionNotificationCenter.Settings;
using TeamsMentionNotificationCenter.Transcript;
using TeamsMentionNotificationCenter.Tray;

namespace TeamsMentionNotificationCenter.Core;

/// <summary>
/// Verdrahtet Transkript-Quelle, Namens-Matcher, Glow-Overlay und Tray-Icon und hält
/// den Zustandsautomaten (Ruhe ↔ Gespräch). Audio-/Musiksteuerung folgt in Phase 2.
/// Läuft auf dem WPF-UI-Thread; Transkript-Events werden dorthin marshallt.
/// </summary>
public sealed class AppController : IDisposable
{
    private readonly AppSettings _settings;
    private readonly Dispatcher _dispatcher;
    private readonly NameMatcher _matcher;
    private readonly GlowOverlay _glow;
    private readonly AudioController _audio;
    private ITranscriptSource? _transcript;
    private TrayIconManager? _tray;
    private HotkeyManager? _hotkeys;
    private SettingsWindow? _settingsWindow;
    private DispatcherTimer? _autoReturnTimer;
    private long _lastOwnSpeechTicks;
    private bool _conversationFromTrigger;
    private bool? _lastLoggedAvailable;

    public AppMode Mode { get; private set; } = AppMode.Quiet;

    public AppController(AppSettings settings, Dispatcher dispatcher)
    {
        _settings = settings;
        _dispatcher = dispatcher;
        _matcher = new NameMatcher(settings);
        _glow = new GlowOverlay(settings);
        _audio = new AudioController(settings);
    }

    public void Start()
    {
        _glow.Build();
        _ = _audio.InitAsync();

        _tray = new TrayIconManager(
            settings: _settings,
            onTestGlow: TestGlow,
            onToggleDetection: ToggleDetection,
            onEnterConversation: () => SetMode(AppMode.Conversation),
            onEnterQuiet: () => SetMode(AppMode.Quiet),
            onOpenSettings: OpenSettingsWindow,
            onReloadSettings: ReloadSettingsFromDisk,
            onExit: () => System.Windows.Application.Current.Shutdown());
        _tray.UpdateStatus(Loc.T("Starte …"), false);
        _tray.SetDetection(_settings.DetectionEnabled);

        _hotkeys = new HotkeyManager();
        _hotkeys.Initialize();
        RegisterHotkeys();
        AutostartManager.Apply(_settings.StartWithWindows);

        _transcript = new UiaTranscriptSource(_settings);
        _transcript.CaptionReceived += OnCaptionReceived;
        _transcript.StatusChanged += OnTranscriptStatusChanged;
        _transcript.Start();

        // Auto-Rückkehr in den Ruhe-Modus, wenn im Gespräch der Name X s nicht mehr fällt.
        _autoReturnTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _autoReturnTimer.Tick += (_, _) => CheckAutoReturn();
        _autoReturnTimer.Start();

        // Startmodus: standardmäßig (manueller) Gesprächs-Modus – der erste Ruhe-Modus muss aktiv gewählt werden.
        SetMode(_settings.StartInConversationMode ? AppMode.Conversation : AppMode.Quiet, fromTrigger: false);
    }

    private void OnCaptionReceived(object? sender, CaptionEventArgs e)
    {
        // Eigene Wortmeldungen für die Auto-Rückkehr merken (unabhängig von der Erkennung).
        if (IsOwnSpeaker(e.Line.Speaker))
        {
            Volatile.Write(ref _lastOwnSpeechTicks, DateTime.UtcNow.Ticks);
            Logger.Log("Eigene Wortmeldung erkannt (Auto-Rückkehr-Timer zurückgesetzt)");
        }

        if (!_settings.DetectionEnabled) return;
        // Bereits im Gesprächs-Modus (manuell ODER durch Trigger): kein erneuter Alarm – man ist ja im
        // Gespräch. Schon vor dem Matcher abbrechen, damit der Cooldown nicht sinnlos scharfgestellt wird
        // und eine Nennung kurz nach der Rückkehr in den Ruhe-Modus nicht verschluckt würde.
        if (Mode == AppMode.Conversation) return;
        if (_matcher.TryMatch(e.Line.Speaker, e.Line.Text, out var word))
        {
            _dispatcher.BeginInvoke(() => OnTriggered(word));
        }
    }

    private bool IsOwnSpeaker(string speaker)
    {
        var own = NameMatcher.Normalize(_settings.OwnSpeakerName);
        if (own.Length == 0) return false;
        var sp = NameMatcher.Normalize(speaker);
        if (sp.Length == 0) return false;
        if (sp == own) return true;

        // Token-Vergleich: alle Tokens des kürzeren Namens müssen im längeren vorkommen
        // (toleriert Mittelinitialen wie "Robert H. Kieschke" vs. "Robert Kieschke").
        var ownTokens = own.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var spTokens = sp.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var (shorter, longer) = ownTokens.Length <= spTokens.Length ? (ownTokens, spTokens) : (spTokens, ownTokens);
        return shorter.Length > 0 && shorter.All(longer.Contains) && shorter.Any(t => t.Length >= 2);
    }

    private void OnTriggered(string word)
    {
        // Autoritative Prüfung auf dem UI-Thread (zwischen Erkennung und Verarbeitung kann der
        // Modus gewechselt haben): im Gesprächs-Modus wird grundsätzlich nicht erneut alarmiert.
        if (Mode == AppMode.Conversation)
        {
            Logger.Log($"TRIGGER unterdrückt (bereits im Gespräch)  Wort='{word}'");
            return;
        }
        Logger.Log($"TRIGGER  Wort='{word}'  Modus={Mode}");
        // Modus ZUERST wechseln (setzt ggf. den Dauer-Rand), Flash ZULETZT – sonst
        // überschreibt die Persistenz-Animation (Opacity->0) sofort das Aufleuchten.
        if (_settings.AutoEnterConversationOnTrigger)
            SetMode(AppMode.Conversation, fromTrigger: true);
        _glow.Flash();
        if (_settings.TriggerSoundEnabled)
            SoundNotifier.Play(_settings.TriggerSoundFile, _settings.TriggerSoundVolume, _settings.TriggerSoundDeviceId);
        _tray?.UpdateStatus(Loc.Tf("Name erkannt: {0}", word), true);
    }

    private void TestGlow()
    {
        Logger.Log("TEST-GLOW ausgelöst");
        _glow.Flash();
    }

    private void OnTranscriptStatusChanged(object? sender, TranscriptStatusEventArgs e)
    {
        if (_lastLoggedAvailable != e.Available) // nur bei Wechsel loggen (kein Poll-Spam)
        {
            _lastLoggedAvailable = e.Available;
            Logger.Log($"STATUS  available={e.Available}  {e.Message}");
        }
        _dispatcher.BeginInvoke(() => _tray?.UpdateStatus(e.Message, e.Available));
    }

    /// <summary>Wechselt den Modus und wendet Glow-Rand + Teams-/Musiksteuerung an.</summary>
    public void SetMode(AppMode mode, bool fromTrigger = false)
    {
        Logger.Log($"MODUS -> {mode}{(fromTrigger ? " (durch Trigger)" : "")}");
        Mode = mode;
        if (mode == AppMode.Conversation)
        {
            _conversationFromTrigger = fromTrigger;
            Volatile.Write(ref _lastOwnSpeechTicks, DateTime.UtcNow.Ticks);
        }
        _glow.SetPersistentBorder(ShouldShowPersistentBorder());
        _tray?.SetMode(mode);
        if (mode == AppMode.Conversation) _audio.ApplyConversation();
        else _audio.ApplyQuiet();
    }

    private bool ShouldShowPersistentBorder() => Mode == AppMode.Conversation && _settings.PersistentBorder switch
    {
        PersistentBorderMode.Always => true,
        PersistentBorderMode.TriggerOnly => _conversationFromTrigger,
        _ => false
    };

    public void ToggleMode() => SetMode(Mode == AppMode.Conversation ? AppMode.Quiet : AppMode.Conversation);

    private void CheckAutoReturn()
    {
        if (!_settings.AutoReturnToQuietEnabled || Mode != AppMode.Conversation) return;
        // Manuell aktivierten Gesprächs-Modus nur zurückholen, wenn ausdrücklich erlaubt.
        if (!_conversationFromTrigger && !_settings.AutoReturnAlsoWhenManual) return;
        var last = Volatile.Read(ref _lastOwnSpeechTicks);
        var idleSeconds = (DateTime.UtcNow - new DateTime(last, DateTimeKind.Utc)).TotalSeconds;
        if (idleSeconds >= Math.Max(1, _settings.AutoReturnAfterSeconds))
        {
            Logger.Log($"Auto-Rückkehr in Ruhe-Modus nach {idleSeconds:F0}s ohne eigene Wortmeldung");
            SetMode(AppMode.Quiet);
        }
    }

    private void RegisterHotkeys()
    {
        if (_hotkeys == null) return;
        _hotkeys.Clear();
        TryRegister(_settings.HotkeyToggle, ToggleMode, "Umschalten");
        TryRegister(_settings.HotkeyQuiet, () => SetMode(AppMode.Quiet), "Ruhe");
        TryRegister(_settings.HotkeyConversation, () => SetMode(AppMode.Conversation), "Gespräch");
        TryRegister(_settings.HotkeyToggleDetection, ToggleDetection, "Erkennung an/aus");
    }

    private void TryRegister(string combo, Action action, string label)
    {
        if (string.IsNullOrWhiteSpace(combo)) return;
        bool ok = _hotkeys!.Register(combo, action);
        Logger.Log($"Hotkey '{combo}' ({label}) {(ok ? "registriert" : "FEHLGESCHLAGEN (evtl. belegt)")}");
    }

    private void ToggleDetection()
    {
        _settings.DetectionEnabled = !_settings.DetectionEnabled;
        _settings.Save();
        _matcher.ResetCooldown();
        _tray?.SetDetection(_settings.DetectionEnabled);
        Logger.Log($"Erkennung {( _settings.DetectionEnabled ? "AN" : "AUS")}");
    }

    private void OpenSettingsWindow()
    {
        if (_settingsWindow != null) { _settingsWindow.Activate(); return; }
        _settingsWindow = new SettingsWindow(_settings,
            onApply: applied =>
            {
                bool langChanged = _settings.Language != applied.Language;
                ApplySettings(applied);
                _settings.Save();
                if (langChanged) // Fenster in neuer Sprache neu aufbauen
                {
                    var w = _settingsWindow;
                    _settingsWindow = null;
                    w?.Close();
                    OpenSettingsWindow();
                }
            },
            onTestGlow: preview => _glow.PreviewWith(preview));
        _settingsWindow.Closed += (_, _) =>
        {
            _settingsWindow = null;
            _glow.Build(); // Vorschau verwerfen und den tatsächlich gespeicherten Stand wiederherstellen
            _glow.SetPersistentBorder(ShouldShowPersistentBorder());
        };
        _settingsWindow.Show();
    }

    /// <summary>Einstellungen von der Platte neu laden und live anwenden.</summary>
    public void ReloadSettingsFromDisk() => ApplySettings(AppSettings.Load());

    /// <summary>Übernimmt geänderte Einstellungen in die laufende App (auch aus der Settings-UI).</summary>
    public void ApplySettings(AppSettings fresh)
    {
        _settings.CopyFrom(fresh);
        Loc.Language = _settings.Language;
        Logger.Enabled = _settings.DebugLogging;
        _matcher.UpdateFrom(_settings);
        _glow.Build(); // neu aufbauen, damit Farbe/Dicke UND Monitor-Auswahl greifen
        _glow.SetPersistentBorder(ShouldShowPersistentBorder());
        _matcher.ResetCooldown();
        RegisterHotkeys();
        AutostartManager.Apply(_settings.StartWithWindows);
        _tray?.Relocalize();
        _tray?.SetMode(Mode);
        _tray?.SetDetection(_settings.DetectionEnabled);
        _tray?.UpdateStatus(Loc.Tf("Einstellungen übernommen ({0} Trigger-Wörter)", _settings.TriggerWords.Count), true);
    }

    public void Dispose()
    {
        if (_transcript != null)
        {
            _transcript.CaptionReceived -= OnCaptionReceived;
            _transcript.StatusChanged -= OnTranscriptStatusChanged;
            _transcript.Dispose();
        }
        _autoReturnTimer?.Stop();
        _hotkeys?.Dispose();
        _glow.Dispose();
        _tray?.Dispose();
        _audio.Dispose();
    }
}
