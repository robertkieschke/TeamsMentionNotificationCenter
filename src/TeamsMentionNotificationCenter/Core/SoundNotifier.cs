using System.IO;
using System.Media;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace TeamsMentionNotificationCenter.Core;

/// <summary>
/// Spielt einen kurzen Signalton (WAV) mit wählbarer Lautstärke und auf einem wählbaren
/// Ausgabegerät ab (NAudio/WASAPI) und listet die verfügbaren Windows-Sounds bzw. Geräte.
/// </summary>
public static class SoundNotifier
{
    private static IWavePlayer? _current; // Referenz halten, damit die Wiedergabe nicht abbricht

    public static string MediaFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media");

    /// <summary>Alle .wav-Dateien aus %WINDIR%\Media als (Anzeigename, Pfad).</summary>
    public static List<(string Display, string Path)> GetAvailableSounds()
    {
        var list = new List<(string, string)>();
        try
        {
            if (Directory.Exists(MediaFolder))
                foreach (var f in Directory.EnumerateFiles(MediaFolder, "*.wav").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                    list.Add((Path.GetFileNameWithoutExtension(f), f));
        }
        catch { /* ignore */ }
        return list;
    }

    /// <summary>Aktive Ausgabegeräte als (Anzeigename, Geräte-ID).</summary>
    public static List<(string Name, string Id)> GetOutputDevices()
    {
        var list = new List<(string, string)>();
        try
        {
            using var en = new MMDeviceEnumerator();
            foreach (var d in en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                list.Add((d.FriendlyName, d.ID));
                d.Dispose();
            }
        }
        catch { /* ignore */ }
        return list;
    }

    public static void Play(string? file, int volumePercent = 100, string? deviceId = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
            {
                SystemSounds.Asterisk.Play();
                return;
            }

            try { _current?.Stop(); } catch { /* vorherige Wiedergabe beenden */ }

            var reader = new AudioFileReader(file) { Volume = Math.Clamp(volumePercent, 0, 100) / 100f };

            MMDevice? device = null;
            if (!string.IsNullOrEmpty(deviceId))
            {
                try { using var en = new MMDeviceEnumerator(); device = en.GetDevice(deviceId); }
                catch { device = null; }
            }

            IWavePlayer output = device != null
                ? new WasapiOut(device, AudioClientShareMode.Shared, false, 200)
                : new WasapiOut();

            output.PlaybackStopped += (_, _) =>
            {
                try { output.Dispose(); } catch { }
                try { reader.Dispose(); } catch { }
                try { device?.Dispose(); } catch { }
            };
            output.Init(reader);
            output.Play();
            _current = output;
        }
        catch
        {
            try { SystemSounds.Asterisk.Play(); } catch { /* ignore */ }
        }
    }
}
