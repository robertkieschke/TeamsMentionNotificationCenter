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
    private readonly CallerBanner _banner;
    private readonly AudioController _audio;
    private readonly MentionStore _mentions;
    private readonly MentionOverlay _mentionOverlay;
    // Nennungen, die noch auf eine eigene Antwort warten (nur UI-Thread).
    private readonly List<(string Speaker, DateTime Utc, DateTime Local)> _pendingMentions = new();
    private long _lastRealOwnSpeechTicks;      // echte eigene Wortmeldungen (unverfälscht von SetMode)
    private volatile bool _hasWaitingPersons;  // es gibt Einträge „warte auf Rückkehr"
    private DateTime _lastMentionCleanupUtc = DateTime.UtcNow;
    private ITranscriptSource? _transcript;
    private TrayIconManager? _tray;
    private HotkeyManager? _hotkeys;
    private SettingsWindow? _settingsWindow;
    private DispatcherTimer? _autoReturnTimer;
    private long _lastOwnSpeechTicks;
    private bool _conversationFromTrigger;
    private bool? _lastLoggedAvailable;

    public AppMode Mode { get; private set; } = AppMode.Quiet;

    /// <summary>Nach einem Update-Neustart wiederherzustellender Modus (aus --resume-mode).</summary>
    public AppMode? ResumeMode { get; set; }

    /// <summary>Nach einem Update-Neustart: Musik war von UNS pausiert (aus --resume-music-paused,
    /// Kompatibilität zu Vorversionen ohne Sitzungs-Liste).</summary>
    public bool ResumeMusicPausedByUs { get; set; }

    /// <summary>Nach einem Update-Neustart: die von UNS pausierten Mediensitzungen (aus --resume-paused).</summary>
    public string[]? ResumePausedSessionIds { get; set; }

    /// <summary>Version, VON der gerade aktualisiert wurde (aus --updated-from) – löst die
    /// einmalige „Was ist neu"-Anzeige aus.</summary>
    public string? UpdatedFromVersion { get; set; }

    public AppController(AppSettings settings, Dispatcher dispatcher)
    {
        _settings = settings;
        _dispatcher = dispatcher;
        _matcher = new NameMatcher(settings);
        _glow = new GlowOverlay(settings);
        _banner = new CallerBanner(settings);
        _audio = new AudioController(settings);
        _mentions = new MentionStore();
        _mentionOverlay = new MentionOverlay(settings, _mentions);
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
            onCheckUpdates: () => CheckForUpdates(manual: true),
            onShowReleaseNotes: () => OpenSettingsWindow(showReleaseNotes: true),
            onShowMissedMentions: () => _mentionOverlay.ShowOverlay(),
            onExit: () => System.Windows.Application.Current.Shutdown());
        _tray.UpdateStatus(Loc.T("Starte …"), false);
        _tray.SetDetection(_settings.DetectionEnabled);

        _hotkeys = new HotkeyManager();
        _hotkeys.Initialize();
        RegisterHotkeys();
        AutostartManager.Apply(_settings.StartWithWindows);

        _mentions.Load(_settings.MentionRetentionDays);
        _mentions.Changed += OnMentionsChanged;

        _transcript = new UiaTranscriptSource(_settings);
        _transcript.CaptionReceived += OnCaptionReceived;
        _transcript.StatusChanged += OnTranscriptStatusChanged;
        _transcript.IncomingCallVisibleChanged += OnIncomingCallChanged;
        _transcript.WatchedPersonSeen += OnWatchedPersonSeen;
        _transcript.Start();
        OnMentionsChanged(this, EventArgs.Empty); // Tray-Zähler + beobachtete Personen initialisieren

        // Auto-Rückkehr in den Ruhe-Modus, wenn im Gespräch der Name X s nicht mehr fällt.
        _autoReturnTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _autoReturnTimer.Tick += (_, _) => CheckAutoReturn();
        _autoReturnTimer.Tick += (_, _) => MentionHousekeeping();
        _autoReturnTimer.Start();

        // Startmodus: nach einem Update-Neustart den vorherigen Zustand wiederherstellen,
        // sonst gemäß Einstellung (Standard: manueller Gesprächs-Modus).
        if (ResumePausedSessionIds is { Length: > 0 }) _audio.RestorePausedSessions(ResumePausedSessionIds);
        else if (ResumeMusicPausedByUs) _audio.MarkLegacyPausedByUs();
        SetMode(ResumeMode ?? (_settings.StartInConversationMode ? AppMode.Conversation : AppMode.Quiet), fromTrigger: false);

        // Verzögert auf Updates prüfen (blockiert den Start nicht; meldet sich nur bei neuer Version).
        if (_settings.CheckUpdatesOnStartup)
        {
            var updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            updateTimer.Tick += (_, _) => { updateTimer.Stop(); CheckForUpdates(manual: false); };
            updateTimer.Start();

            // Im Silent-Modus zusätzlich periodisch prüfen – die App läuft oft tage-/wochenlang im Tray.
            var periodicTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(6) };
            periodicTimer.Tick += (_, _) => { if (_settings.SilentAutoUpdate) CheckForUpdates(manual: false); };
            periodicTimer.Start();
        }

        // Noch offene verpasste Erwähnungen aus der letzten Sitzung wieder anzeigen.
        if (_settings.MissedMentionsEnabled && _mentions.UnfinishedCount > 0)
            _mentionOverlay.ShowOverlay();

        // Nach einem Update einmalig die Versionshinweise der neuen Version anzeigen.
        if (UpdatedFromVersion != null && _settings.ShowNotesAfterUpdate)
        {
            var notesTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            notesTimer.Tick += (_, _) => { notesTimer.Stop(); _ = ShowReleaseNotesAsync(); };
            notesTimer.Start();
        }
    }

    private async Task ShowReleaseNotesAsync()
    {
        try
        {
            var notes = await UpdateManager.GetReleaseNotesAsync(AppInfo.Version);
            if (notes == null || string.IsNullOrWhiteSpace(notes.Value.Body))
            {
                Logger.Log("Versionshinweise: kein Release(-Text) zur aktuellen Version gefunden.");
                return;
            }
            Logger.Log($"Zeige Versionshinweise für {notes.Value.Tag} (Update von {UpdatedFromVersion}).");
            UpdateManager.ShowNotesWindow(notes.Value.Tag, notes.Value.Body, notes.Value.Url);
        }
        catch (Exception ex)
        {
            Logger.Log("Versionshinweise fehlgeschlagen: " + ex.Message);
        }
    }

    /// <summary>Argumente für den Neustart nach einem Update, damit die neue Instanz den
    /// aktuellen Zustand nahtlos wiederherstellt.</summary>
    private string BuildResumeArguments()
    {
        var args = $"--resume-mode={Mode} --updated-from={AppInfo.Version}";
        if (_audio.PausedSessionIds is { Length: > 0 } paused)
            args += " --resume-paused=\"" + string.Join(";", paused) + "\"";
        return args;
    }

    /// <summary>Update-Prüfung. Bei manueller Prüfung (Tray) gibt es IMMER eine Rückmeldung,
    /// beim Start-Check nur, wenn tatsächlich eine neue Version existiert.</summary>
    private async void CheckForUpdates(bool manual)
    {
        try
        {
            var release = await UpdateManager.CheckAsync();
            if (release == null)
            {
                if (manual)
                    Theme.ShowMessage(Loc.Tf("Du verwendest bereits die neueste Version ({0}).", AppInfo.Version));
                return;
            }

            Logger.Log($"Update verfügbar: {release.Tag} (installiert: {AppInfo.Version})");

            if (!manual && _settings.SilentAutoUpdate)
            {
                // Silent-Autoupdate: ohne Nachfrage im Hintergrund installieren; der aktuelle
                // Zustand (Modus, Musik-Merker) wird über den Neustart hinweg wiederhergestellt.
                Logger.Log("Silent-Update: Installation im Hintergrund ohne Nachfrage.");
                _tray?.UpdateStatus(Loc.T("Update wird im Hintergrund installiert …"), true);
            }
            else if (!UpdateManager.AskUser(release))
            {
                return; // „Später" – beim nächsten Start/der nächsten Prüfung erneut
            }

            await UpdateManager.DownloadAndRestartAsync(release,
                status => _tray?.UpdateStatus(status, true),
                releaseSingleInstance: () => (System.Windows.Application.Current as App)?.ReleaseSingleInstanceMutex(),
                restartArguments: BuildResumeArguments());
        }
        catch (Exception ex)
        {
            Logger.Log($"Update-Prüfung/-Installation fehlgeschlagen: {ex.Message}");
            if (manual)
                Theme.ShowMessage(Loc.Tf("Update fehlgeschlagen: {0}", ex.Message), warning: true);
        }
    }

    private void OnCaptionReceived(object? sender, CaptionEventArgs e)
    {
        // Eigene Wortmeldungen für die Auto-Rückkehr merken (unabhängig von der Erkennung).
        if (IsOwnSpeaker(e.Line.Speaker))
        {
            Volatile.Write(ref _lastOwnSpeechTicks, DateTime.UtcNow.Ticks);
            Volatile.Write(ref _lastRealOwnSpeechTicks, DateTime.UtcNow.Ticks); // „beantwortet"-Prüfung
            Logger.Log("Eigene Wortmeldung erkannt (Auto-Rückkehr-Timer zurückgesetzt)");
        }
        else if (_hasWaitingPersons && !string.IsNullOrWhiteSpace(e.Line.Speaker))
        {
            // Fallback der Bei-Rückkehr-Erinnerung: Die Person spricht wieder.
            var seenSpeaker = e.Line.Speaker;
            _dispatcher.BeginInvoke(() => HandlePersonSeen(seenSpeaker));
        }

        if (!_settings.DetectionEnabled) return;
        // Bereits im Gesprächs-Modus (manuell ODER durch Trigger): kein erneuter Alarm – man ist ja im
        // Gespräch. Schon vor dem Matcher abbrechen, damit der Cooldown nicht sinnlos scharfgestellt wird
        // und eine Nennung kurz nach der Rückkehr in den Ruhe-Modus nicht verschluckt würde.
        if (Mode == AppMode.Conversation) return;
        if (_matcher.TryMatch(e.Line.Speaker, e.Line.Text, out var word))
        {
            var speaker = e.Line.Speaker;
            _dispatcher.BeginInvoke(() => OnTriggered(word, speaker));
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

    private void OnTriggered(string word, string speaker)
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
        if (_settings.BannerEnabled)
            _banner.Show(speaker);
        if (_settings.TriggerSoundEnabled)
            SoundNotifier.Play(_settings.TriggerSoundFile, _settings.TriggerSoundVolume, _settings.TriggerSoundDeviceId);
        _tray?.UpdateStatus(Loc.Tf("Name erkannt: {0}", word), true);

        // Kandidat für „verpasste Erwähnung": wird nach Ablauf des Antwort-Timeouts zum Eintrag,
        // wenn bis dahin keine eigene Wortmeldung kam (MentionHousekeeping).
        if (_settings.MissedMentionsEnabled)
            _pendingMentions.Add((speaker, DateTime.UtcNow, DateTime.Now));
    }

    /// <summary>Sekündliche Pflege der verpassten Erwähnungen: Antwort-Timeouts auflösen,
    /// fällige Erinnerungen wieder öffnen, stündlich alte Einträge aufräumen.</summary>
    private void MentionHousekeeping()
    {
        var nowUtc = DateTime.UtcNow;

        if (_pendingMentions.Count > 0)
        {
            long lastOwn = Volatile.Read(ref _lastRealOwnSpeechTicks);
            int timeout = Math.Max(5, _settings.MentionAnswerTimeoutSeconds);
            for (int i = _pendingMentions.Count - 1; i >= 0; i--)
            {
                var pending = _pendingMentions[i];
                if (lastOwn > pending.Utc.Ticks)
                {
                    _pendingMentions.RemoveAt(i); // beantwortet -> kein Eintrag
                }
                else if ((nowUtc - pending.Utc).TotalSeconds >= timeout)
                {
                    _pendingMentions.RemoveAt(i);
                    var entry = _mentions.AddIfNew(pending.Speaker, pending.Local, _settings.MentionRepeatMinutes);
                    if (entry != null)
                    {
                        Logger.Log($"Verpasste Erwähnung ({entry.MentionedAt:HH:mm}): keine Antwort innerhalb {timeout}s");
                        _mentionOverlay.ShowOverlay();
                    }
                }
            }
        }

        var due = _mentions.TickSnoozes(DateTime.Now);
        if (due.Count > 0)
        {
            Logger.Log($"Erinnerung fällig: {due.Count} Eintrag/Einträge wieder offen");
            _mentionOverlay.ShowOverlay();
        }

        if ((nowUtc - _lastMentionCleanupUtc).TotalHours >= 1)
        {
            _lastMentionCleanupUtc = nowUtc;
            _mentions.Cleanup(_settings.MentionRetentionDays);
        }
    }

    private void OnWatchedPersonSeen(object? sender, string name) =>
        _dispatcher.BeginInvoke(() => HandlePersonSeen(name));

    private void HandlePersonSeen(string speaker)
    {
        var reopened = _mentions.PersonSeen(speaker);
        if (reopened.Count == 0) return;
        Logger.Log($"Person wieder im Call -> {reopened.Count} Eintrag/Einträge wieder offen");
        _mentionOverlay.ShowOverlay();
    }

    private void OnMentionsChanged(object? sender, EventArgs e)
    {
        _hasWaitingPersons = _mentions.WaitingSpeakers.Length > 0;
        _transcript?.SetWatchedPersons(_mentions.WaitingSpeakers);
        _tray?.SetMissedCount(_mentions.UnfinishedCount);
    }

    private void TestGlow()
    {
        Logger.Log("TEST-GLOW ausgelöst");
        _glow.Flash();
    }

    private void OnIncomingCallChanged(object? sender, bool visible)
    {
        if (!visible) return; // nur auf das ERSCHEINEN des Anruf-Popups reagieren
        if (!_settings.DetectionEnabled || !_settings.EnterConversationOnIncomingCall) return;
        _dispatcher.BeginInvoke(() =>
        {
            if (Mode == AppMode.Conversation) return;
            Logger.Log("Eingehender Anruf -> Gesprächs-Modus (Klingeln wird laut)");
            SetMode(AppMode.Conversation, fromTrigger: true);
            _tray?.UpdateStatus(Loc.T("Eingehender Anruf – Gesprächs-Modus aktiviert"), true);
        });
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
        TryRegister(_settings.HotkeyShowMissed, ShowMissedOverlay, "Verpasste Erwähnungen");
    }

    private void ShowMissedOverlay()
    {
        if (_mentions.UnfinishedCount > 0)
        {
            Logger.Log($"Hotkey: Verpasst-Overlay geöffnet ({_mentions.UnfinishedCount} offen)");
            _mentionOverlay.ShowOverlay();
        }
        else
        {
            Logger.Log("Hotkey: keine verpassten Erwähnungen (Feedback im Tray)");
            _tray?.UpdateStatus(Loc.T("Keine verpassten Erwähnungen."), true); // Feedback statt stillem Nichts
        }
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

    private void OpenSettingsWindow() => OpenSettingsWindow(showReleaseNotes: false);

    private void OpenSettingsWindow(bool showReleaseNotes)
    {
        if (_settingsWindow != null)
        {
            if (showReleaseNotes) _settingsWindow.ShowReleaseNotesTab();
            if (_settingsWindow.WindowState == System.Windows.WindowState.Minimized)
                _settingsWindow.WindowState = System.Windows.WindowState.Normal;
            _settingsWindow.Activate();
            BringSettingsToForeground();
            return;
        }
        _settingsWindow = new SettingsWindow(_settings, _mentions,
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
            onTestGlow: preview => _glow.PreviewWith(preview),
            onTestBanner: preview => _banner.PreviewWith(preview));
        _settingsWindow.Closed += (_, _) =>
        {
            _settingsWindow = null;
            _glow.Build(); // Vorschau verwerfen und den tatsächlich gespeicherten Stand wiederherstellen
            _glow.SetPersistentBorder(ShouldShowPersistentBorder());
        };
        if (showReleaseNotes) _settingsWindow.ShowReleaseNotesTab(); // auch beim FRISCH geöffneten Fenster
        _settingsWindow.Show();
        BringSettingsToForeground();
    }

    /// <summary>Holt das Einstellungsfenster vor die Vordergrundsperre – aus einer Tray-App heraus
    /// öffnen sich Fenster sonst im Hintergrund (nur der Taskleisten-Eintrag blinkt).</summary>
    private void BringSettingsToForeground()
    {
        if (_settingsWindow == null) return;
        var hwnd = new System.Windows.Interop.WindowInteropHelper(_settingsWindow).Handle;
        Interop.NativeMethods.ForceForeground(hwnd);
    }

    /// <summary>Einstellungen von der Platte neu laden und live anwenden.</summary>
    public void ReloadSettingsFromDisk() => ApplySettings(AppSettings.Load());

    /// <summary>Übernimmt geänderte Einstellungen in die laufende App (auch aus der Settings-UI).</summary>
    public void ApplySettings(AppSettings fresh)
    {
        _settings.CopyFrom(fresh);
        Loc.Language = AppSettings.ResolveLanguage(_settings.Language);
        Logger.Enabled = _settings.DebugLogging;
        Theme.Apply(_settings.Theme);
        _matcher.UpdateFrom(_settings);
        _glow.Build(); // neu aufbauen, damit Farbe/Dicke UND Monitor-Auswahl greifen
        _glow.SetPersistentBorder(ShouldShowPersistentBorder());
        _matcher.ResetCooldown();
        RegisterHotkeys();
        AutostartManager.Apply(_settings.StartWithWindows);
        _mentions.Cleanup(_settings.MentionRetentionDays);
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
            _transcript.IncomingCallVisibleChanged -= OnIncomingCallChanged;
            _transcript.WatchedPersonSeen -= OnWatchedPersonSeen;
            _transcript.Dispose();
        }
        _autoReturnTimer?.Stop();
        _hotkeys?.Dispose();
        _glow.Dispose();
        _banner.Dispose();
        _mentionOverlay.Dispose();
        _tray?.Dispose();
        _audio.Dispose();
    }
}
