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
    private volatile bool _musicPausedByUs;

    public AudioController(AppSettings settings) => _settings = settings;

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
    private async Task PauseMusicAsync()
    {
        var session = await GetMusicSessionAsync();
        if (session == null) return;
        try
        {
            var status = session.GetPlaybackInfo().PlaybackStatus;
            if (status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing &&
                await session.TryPauseAsync())
            {
                _musicPausedByUs = true;
            }
        }
        catch { }
    }

    private async Task ResumeMusicAsync()
    {
        if (!_musicPausedByUs) return;       // nur fortsetzen, was wir selbst pausiert haben
        _musicPausedByUs = false;
        var session = await GetMusicSessionAsync();
        if (session == null) return;
        try { await session.TryPlayAsync(); }
        catch { }
    }

    private async Task<GlobalSystemMediaTransportControlsSession?> GetMusicSessionAsync()
    {
        try
        {
            _smtc ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_smtc == null) return null;

            var hint = _settings.MusicAppHint ?? "";
            if (hint.Length > 0)
            {
                foreach (var s in _smtc.GetSessions())
                    if ((s.SourceAppUserModelId ?? "").Contains(hint, StringComparison.OrdinalIgnoreCase))
                        return s;
            }
            return _smtc.GetCurrentSession(); // Fallback: aktive Medien-Session
        }
        catch { return null; }
    }

    public void Dispose() { /* nichts zu entsorgen */ }
}
