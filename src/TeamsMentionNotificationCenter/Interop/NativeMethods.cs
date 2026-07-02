using System.Runtime.InteropServices;

namespace TeamsMentionNotificationCenter.Interop;

/// <summary>Win32-Interop für das klick-durchlässige, monitor-genau positionierte Overlay.</summary>
internal static class NativeMethods
{
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    // 32-Bit-Varianten reichen für die Extended-Style-Bits und laufen auf x86 wie x64.
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    /// <summary>Alle Monitor-Rechtecke in physischen Pixeln.</summary>
    public static List<RECT> GetMonitorRects()
    {
        var result = new List<RECT>();
        MonitorEnumProc cb = (IntPtr _, IntPtr _, ref RECT r, IntPtr _) =>
        {
            result.Add(r);
            return true;
        };
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, cb, IntPtr.Zero);
        GC.KeepAlive(cb);
        return result;
    }

    [DllImport("user32.dll")]
    public static extern uint GetDpiForSystem();

    /// <summary>System-DPI-Skalierung (1.0 = 100 %). Für gleichmäßige DPI über alle Monitore korrekt.</summary>
    public static double GetSystemScale()
    {
        try
        {
            uint dpi = GetDpiForSystem();
            return dpi == 0 ? 1.0 : dpi / 96.0;
        }
        catch { return 1.0; }
    }

    public static void MakeClickThrough(IntPtr hwnd)
    {
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        ex |= WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        SetWindowLong(hwnd, GWL_EXSTYLE, ex);
    }
}
