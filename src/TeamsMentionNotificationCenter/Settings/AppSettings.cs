using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TeamsMentionNotificationCenter.Localization;

namespace TeamsMentionNotificationCenter.Settings;

/// <summary>Verhalten des Teams-Tons im Ruhe-Modus.</summary>
public enum QuietBehavior
{
    /// <summary>Teams auf ein leises Niveau absenken (Standard – Sicherheitsnetz).</summary>
    Lower,
    /// <summary>Teams komplett stummschalten.</summary>
    Mute
}

/// <summary>Welche Transkript-Quelle verwendet wird.</summary>
public enum TranscriptSourceKind
{
    Auto,
    Uia,
    Ocr
}

/// <summary>Wann der dezente Dauer-Rand im Gesprächs-Modus gezeigt wird.</summary>
public enum PersistentBorderMode
{
    /// <summary>Nie.</summary>
    Never,
    /// <summary>Nur, wenn der Gesprächs-Modus durch eine Namensnennung ausgelöst wurde.</summary>
    TriggerOnly,
    /// <summary>Immer im Gesprächs-Modus (auch bei manueller Aktivierung).</summary>
    Always
}

/// <summary>
/// Alle konfigurierbaren Einstellungen. Wird als JSON unter
/// %APPDATA%\TeamsMentionNotificationCenter\settings.json gespeichert und ist vollständig über die
/// In-App-Oberfläche editierbar (multi-user-fähig; kein hartkodierter Name).
/// </summary>
public sealed class AppSettings
{
    // --- Namens-/Trigger-Erkennung ---
    public List<string> TriggerWords { get; set; } = new();
    public bool FuzzyEnabled { get; set; } = true;
    /// <summary>Maximale Levenshtein-Distanz für Fuzzy-Treffer (Erkennungsfehler abfangen).</summary>
    public int FuzzyMaxDistance { get; set; } = 1;
    /// <summary>Nicht auslösen, wenn man selbst spricht.</summary>
    public bool IgnoreOwnSpeaker { get; set; } = true;
    /// <summary>Eigener (Sprecher-)Name, wie er im Transkript erscheint.</summary>
    public string OwnSpeakerName { get; set; } = "";
    /// <summary>Mindestabstand zwischen zwei Auslösungen (ms), gegen Dauerfeuer.</summary>
    public int TriggerCooldownMs { get; set; } = 8000;

    // --- Ton / Musik ---
    public QuietBehavior QuietBehavior { get; set; } = QuietBehavior.Lower;
    /// <summary>Teams-Lautstärke im Ruhe-Modus (%), wenn QuietBehavior=Lower.</summary>
    public int QuietLevelPercent { get; set; } = 15;
    /// <summary>Teams-Lautstärke im Gesprächs-Modus (%).</summary>
    public int ConversationLevelPercent { get; set; } = 100;
    /// <summary>Teilstring der Medien-App (SMTC AppUserModelId), z. B. "Spotify".</summary>
    public string MusicAppHint { get; set; } = "Spotify";

    // --- Automatische Aktionen bei Trigger (jede einzeln schaltbar) ---
    public bool AutoEnterConversationOnTrigger { get; set; } = true;
    public bool RaiseTeamsOnTrigger { get; set; } = true;
    public bool PauseMusicOnTrigger { get; set; } = true;

    // --- Rückkehr in den Ruhe-Modus ---
    public bool AutoReturnToQuietEnabled { get; set; } = false;
    public int AutoReturnAfterSeconds { get; set; } = 30;
    /// <summary>Auto-Rückkehr auch anwenden, wenn der Gesprächs-Modus MANUELL (Shortcut/Menü) aktiviert wurde.</summary>
    public bool AutoReturnAlsoWhenManual { get; set; } = false;

    // --- Glow-Overlay ---
    public string GlowColorHex { get; set; } = "#FF3B30";
    public int GlowDurationMs { get; set; } = 1600;
    public double GlowThickness { get; set; } = 26;
    /// <summary>Zu beleuchtende Monitore (0-basierte Indizes in Enumerationsreihenfolge). Leer = alle.</summary>
    public List<int> GlowMonitors { get; set; } = new();
    /// <summary>Wann der dezente Dauer-Rand im Gesprächs-Modus gezeigt wird.</summary>
    public PersistentBorderMode PersistentBorder { get; set; } = PersistentBorderMode.TriggerOnly;

    // --- Signalton bei Erkennung ---
    public bool TriggerSoundEnabled { get; set; } = false;
    /// <summary>Pfad zur abzuspielenden WAV-Datei (leer = Windows-Standardton).</summary>
    public string TriggerSoundFile { get; set; } = "";
    /// <summary>Lautstärke des Signaltons in Prozent (0–100).</summary>
    public int TriggerSoundVolume { get; set; } = 100;
    /// <summary>ID des Ausgabegeräts für den Signalton (leer = Standardgerät).</summary>
    public string TriggerSoundDeviceId { get; set; } = "";

    // --- Hotkeys (Phase 3) ---
    public string HotkeyToggle { get; set; } = "Ctrl+Alt+T";
    public string HotkeyQuiet { get; set; } = "Ctrl+Alt+Q";
    public string HotkeyConversation { get; set; } = "Ctrl+Alt+G";
    public string HotkeyToggleDetection { get; set; } = "Ctrl+Alt+E";

    // --- Quelle / Start ---
    public TranscriptSourceKind TranscriptSource { get; set; } = TranscriptSourceKind.Auto;
    /// <summary>Poll-Intervall für die Transkript-Quelle (ms).</summary>
    public int PollIntervalMs { get; set; } = 500;
    public bool StartWithWindows { get; set; } = false;
    /// <summary>Master-Schalter: Erkennung aktiv?</summary>
    public bool DetectionEnabled { get; set; } = true;

    /// <summary>Beim Start im (manuellen) Gesprächs-Modus starten; der erste Ruhe-Modus muss aktiv gewählt werden.</summary>
    public bool StartInConversationMode { get; set; } = true;

    /// <summary>UI-Sprache.</summary>
    public AppLanguage Language { get; set; } = AppLanguage.De;

    /// <summary>Nicht-inhaltliches Debug-Log nach %APPDATA%\TeamsMentionNotificationCenter\log.txt (zur Fehlersuche).</summary>
    public bool DebugLogging { get; set; } = false;

    // ------------------------------------------------------------------
    [JsonIgnore]
    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TeamsMentionNotificationCenter");

    [JsonIgnore]
    public static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static AppSettings Load()
    {
        TryMigrateLegacySettings();
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var s = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (s != null) return s;
            }
        }
        catch
        {
            // Beschädigte Datei -> Defaults verwenden (überschreibt erst beim nächsten Save).
        }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }

    /// <summary>Einmalige Migration: Einstellungen aus dem früheren Ordner „TeamsSound" übernehmen.</summary>
    private static void TryMigrateLegacySettings()
    {
        try
        {
            if (File.Exists(SettingsPath)) return;
            var legacy = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TeamsSound", "settings.json");
            if (File.Exists(legacy))
            {
                Directory.CreateDirectory(SettingsDirectory);
                File.Copy(legacy, SettingsPath);
            }
        }
        catch { /* Migration ist best effort */ }
    }

    /// <summary>Übernimmt alle Werte aus <paramref name="s"/> in DIESE Instanz, damit alle
    /// Komponenten, die dieselbe Instanz referenzieren, die neuen Werte sehen (Live-Reload).</summary>
    public void CopyFrom(AppSettings s)
    {
        TriggerWords = s.TriggerWords;
        FuzzyEnabled = s.FuzzyEnabled;
        FuzzyMaxDistance = s.FuzzyMaxDistance;
        IgnoreOwnSpeaker = s.IgnoreOwnSpeaker;
        OwnSpeakerName = s.OwnSpeakerName;
        TriggerCooldownMs = s.TriggerCooldownMs;
        QuietBehavior = s.QuietBehavior;
        QuietLevelPercent = s.QuietLevelPercent;
        ConversationLevelPercent = s.ConversationLevelPercent;
        MusicAppHint = s.MusicAppHint;
        AutoEnterConversationOnTrigger = s.AutoEnterConversationOnTrigger;
        RaiseTeamsOnTrigger = s.RaiseTeamsOnTrigger;
        PauseMusicOnTrigger = s.PauseMusicOnTrigger;
        AutoReturnToQuietEnabled = s.AutoReturnToQuietEnabled;
        AutoReturnAfterSeconds = s.AutoReturnAfterSeconds;
        AutoReturnAlsoWhenManual = s.AutoReturnAlsoWhenManual;
        GlowColorHex = s.GlowColorHex;
        GlowDurationMs = s.GlowDurationMs;
        GlowThickness = s.GlowThickness;
        GlowMonitors = s.GlowMonitors;
        PersistentBorder = s.PersistentBorder;
        HotkeyToggle = s.HotkeyToggle;
        HotkeyQuiet = s.HotkeyQuiet;
        HotkeyConversation = s.HotkeyConversation;
        HotkeyToggleDetection = s.HotkeyToggleDetection;
        TranscriptSource = s.TranscriptSource;
        PollIntervalMs = s.PollIntervalMs;
        StartWithWindows = s.StartWithWindows;
        DetectionEnabled = s.DetectionEnabled;
        StartInConversationMode = s.StartInConversationMode;
        Language = s.Language;
        TriggerSoundEnabled = s.TriggerSoundEnabled;
        TriggerSoundFile = s.TriggerSoundFile;
        TriggerSoundVolume = s.TriggerSoundVolume;
        TriggerSoundDeviceId = s.TriggerSoundDeviceId;
        DebugLogging = s.DebugLogging;
    }
}
