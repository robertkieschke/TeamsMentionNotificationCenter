using System.IO;
using TeamsMentionNotificationCenter.Settings;

namespace TeamsMentionNotificationCenter.Core;

/// <summary>
/// Sehr einfaches, abschaltbares Debug-Log (%APPDATA%\TeamsMentionNotificationCenter\log.txt).
/// Protokolliert bewusst NUR nicht-inhaltliche Diagnose (Status, Anzahlen, getroffenes
/// Trigger-Wort) – niemals gesprochenen Text oder fremde Sprechernamen.
/// </summary>
public static class Logger
{
    private static readonly object Gate = new();

    public static bool Enabled { get; set; }

    public static string Path => System.IO.Path.Combine(AppSettings.SettingsDirectory, "log.txt");

    public static void Log(string message)
    {
        if (!Enabled) return;
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppSettings.SettingsDirectory);
                File.AppendAllText(Path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}");
            }
        }
        catch { /* Logging darf nie stören */ }
    }
}
