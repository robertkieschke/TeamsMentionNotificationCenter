using System.Diagnostics;
using Microsoft.Win32;

namespace TeamsMentionNotificationCenter.Core;

/// <summary>Autostart mit Windows über den HKCU-Run-Schlüssel (kein Admin nötig).</summary>
public static class AutostartManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TeamsMentionNotificationCenter";

    public static void Apply(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (key == null) return;

            if (enable)
            {
                var exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exe)) key.SetValue(ValueName, $"\"{exe}\"");
            }
            else if (key.GetValue(ValueName) != null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch { /* Registry nicht verfügbar -> ignorieren */ }
    }
}
