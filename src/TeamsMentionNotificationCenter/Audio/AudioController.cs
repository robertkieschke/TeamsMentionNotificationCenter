using System.Diagnostics;
using NAudio.CoreAudioApi;
using TeamsMentionNotificationCenter.Core;
using TeamsMentionNotificationCenter.Settings;
using Windows.Media.Control;

namespace TeamsMentionNotificationCenter.Audio;

/// <summary>
/// Steuert den Teams-App-Ton (NAudio / CoreAudio, pro-App-Lautstärke wie EarTrumpet) und
/// die Musik-Wiedergabe (WinRT SMTC, i. d. R. Spotify).
///
/// - <see cref="ApplyConversation"/>: Teams laut, Musik pausieren.
/// - <see cref="ApplyQuiet"/>: Teams leise/stumm, Musik fortsetzen (nur, wenn WIR sie pausiert haben).
///
/// Die Schalter <c>RaiseTeamsOnTrigger</c> bzw. <c>PauseMusicOnTrigger</c> wirken als Haupt-
/// schalter dafür, ob der Teams-Ton bzw. die Musik überhaupt gesteuert werden.
/// Audio-Operationen sind schnell/asynchron und blockieren den UI-Thread nicht.
/// </summary>
public sealed class AudioController : IDisposable
{
    private readonly AppSettings _settings;
    private GlobalSystemMediaTransportControlsSessionManager? _smtc;
    // AppUserModelIds der Sitzungen, die WIR pausiert haben – genau diese werden fortgesetzt.
    private readonly List<string> _pausedByUs = new();
    private bool _legacyResumeCurrent; // Update-Neustart aus einer Vorversion: nur „irgendwas war pausiert" bekannt

    public AudioController(AppSettings settings) => _settings = settings;

    /// <summary>Von UNS pausierte Sitzungen (für die Zustands-Übergabe beim Update-Neustart).</summary>
    public string[] PausedSessionIds => _pausedByUs.ToArray();

    /// <summary>Stellt die Pausiert-von-uns-Liste nach einem Update-Neustart wieder her.</summary>
    public void RestorePausedSessions(IEnumerable<string> ids)
    {
        _pausedByUs.Clear();
        _pausedByUs.AddRange(ids.Where(id => !string.IsNullOrWhiteSpace(id)));
    }

    /// <summary>Kompatibilität: Vorversionen übergeben nur ein Flag statt der Sitzungs-Liste.</summary>
    public void MarkLegacyPausedByUs() => _legacyResumeCurrent = true;

    /// <summary>Passt die Quelle zum Filter? Leerer Filter = alle Quellen.</summary>
    public static bool SourceMatchesFilter(string appUserModelId, IReadOnlyCollection<string> filter) =>
        filter.Count == 0 ||
        filter.Any(f => !string.IsNullOrWhiteSpace(f) &&
                        (appUserModelId ?? "").Contains(f.Trim(), StringComparison.OrdinalIgnoreCase));

    public async Task InitAsync()
    {
        try { _smtc = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync(); }
        catch { _smtc = null; }
    }

    public void ApplyConversation()
    {
        if (_settings.RaiseTeamsOnTrigger)
            SetTeamsVolume(Math.Clamp(_settings.ConversationLevelPercent, 0, 100) / 100f, mute: false);
        if (_settings.PauseMusicOnTrigger)
            _ = PauseMusicAsync();
    }

    public void ApplyQuiet()
    {
        if (_settings.RaiseTeamsOnTrigger)
        {
            if (_settings.QuietBehavior == QuietBehavior.Mute)
                SetTeamsVolume(null, mute: true);
            else
                SetTeamsVolume(Math.Clamp(_settings.QuietLevelPercent, 0, 100) / 100f, mute: false);
        }
        if (_settings.PauseMusicOnTrigger)
            _ = ResumeMusicAsync();
    }

    // --- Teams-Ton (pro-App) ---------------------------------------------
    private void SetTeamsVolume(float? level, bool mute)
    {
        try
        {
            var pids = Process.GetProcessesByName("ms-teams").Select(p => (uint)p.Id).ToHashSet();
            if (pids.Count == 0) return;

            // WICHTIG: über ALLE aktiven Wiedergabegeräte gehen, nicht nur das Standardgerät.
            // Headsets wie das Razer Nari melden mehrere Endpunkte („Game" + „Chat"), und Teams gibt
            // als Kommunikations-App i. d. R. auf dem Chat-Endpunkt aus – seine Audio-Session hängt
            // dann an einem anderen Gerät als dem Multimedia-Standard.
            // AUSNAHME: vom Nutzer ausgeschlossene Geräte (z. B. das Gerät des Teams-„Zweiten
            // Rufsignals") werden NIE angefasst – so bleibt das Klingeln zusätzlicher Anrufe laut.
            var excluded = _settings.AudioExcludedDeviceIds;
            int hits = 0, deviceCount = 0, skipped = 0;
            using var enumerator = new MMDeviceEnumerator();
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                using (device)
                {
                    try
                    {
                        if (excluded.Contains(device.ID, StringComparer.OrdinalIgnoreCase)) { skipped++; continue; }
                        deviceCount++;
                        var sessions = device.AudioSessionManager.Sessions;
                        for (int i = 0; i < sessions.Count; i++)
                        {
                            var s = sessions[i];
                            try
                            {
                                if (!pids.Contains(s.GetProcessID)) continue; // alle ms-teams-Sessions treffen
                                s.SimpleAudioVolume.Mute = mute;
                                if (level.HasValue) s.SimpleAudioVolume.Volume = level.Value;
                                hits++;
                            }
                            catch { /* einzelne (abgelaufene) Session ignorieren */ }
                        }
                    }
                    catch { /* defekten/virtuellen Endpunkt ignorieren, weiter mit dem nächsten */ }
                }
            }
            Logger.Log($"Teams-Ton: {hits} Session(s) auf {deviceCount} Wiedergabegerät(en) angepasst, " +
                       $"{skipped} Gerät(e) ausgeschlossen (mute={mute}, level={(level.HasValue ? (int)(level.Value * 100) + "%" : "-")})");
        }
        catch { /* kein Ausgabegerät / keine Session -> ignorieren */ }
    }

    // --- Musik (SMTC) -----------------------------------------------------
    // „Pausiere, was gerade spielt": ALLE spielenden Mediensitzungen (Spotify, YouTube/Amazon
    // Music im Browser, …) werden pausiert und gemerkt – fortgesetzt wird später GENAU diese
    // Liste. Der optionale Filter (Mehrfachauswahl) beschränkt das auf ausgewählte Quellen.
    private async Task PauseMusicAsync()
    {
        try
        {
            _smtc ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_smtc == null) return;

            var filter = _settings.MusicAppFilter ?? new List<string>();
            int paused = 0;
            foreach (var session in _smtc.GetSessions())
            {
                var id = session.SourceAppUserModelId ?? "";
                if (!SourceMatchesFilter(id, filter)) continue;
                if (session.GetPlaybackInfo().PlaybackStatus !=
                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing) continue;
                if (await session.TryPauseAsync())
                {
                    paused++;
                    if (!_pausedByUs.Contains(id, StringComparer.OrdinalIgnoreCase))
                        _pausedByUs.Add(id);
                }
            }
            if (paused > 0)
                Logger.Log($"Musik: {paused} Wiedergabe(n) pausiert ({string.Join(", ", _pausedByUs)})");
        }
        catch { /* SMTC nicht verfügbar -> ignorieren */ }
    }

    private async Task ResumeMusicAsync()
    {
        try
        {
            bool legacy = _legacyResumeCurrent;
            _legacyResumeCurrent = false;
            var ids = _pausedByUs.ToArray();
            _pausedByUs.Clear();
            if (ids.Length == 0 && !legacy) return; // nur fortsetzen, was wir selbst pausiert haben

            _smtc ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_smtc == null) return;

            int resumed = 0;
            if (ids.Length > 0)
            {
                var sessions = _smtc.GetSessions();
                foreach (var id in ids)
                {
                    var session = sessions.FirstOrDefault(s =>
                        string.Equals(s.SourceAppUserModelId, id, StringComparison.OrdinalIgnoreCase));
                    if (session != null && await session.TryPlayAsync()) resumed++;
                }
            }
            else if (legacy)
            {
                var current = _smtc.GetCurrentSession();
                if (current != null && await current.TryPlayAsync()) resumed++;
            }
            if (resumed > 0) Logger.Log($"Musik: {resumed} Wiedergabe(n) fortgesetzt");
        }
        catch { /* Quelle inzwischen weg (z. B. Tab geschlossen) -> ignorieren */ }
    }

    public void Dispose() { /* nichts zu entsorgen */ }
}
