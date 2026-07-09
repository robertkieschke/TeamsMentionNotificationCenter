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

    private const uint SPI_GETWORKAREA = 0x0030;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);

    /// <summary>Arbeitsbereich des PRIMÄRmonitors in physischen Pixeln – frisch vom System
    /// (kein WPF-Cache; nach Standby/Monitorwechseln zuverlässig).</summary>
    public static RECT GetPrimaryWorkArea()
    {
        var r = new RECT();
        if (SystemParametersInfo(SPI_GETWORKAREA, 0, ref r, 0) && r.Width > 0 && r.Height > 0)
            return r;
        double scale = GetSystemScale();
        return new RECT
        {
            Left = 0,
            Top = 0,
            Right = (int)(System.Windows.SystemParameters.PrimaryScreenWidth * scale),
            Bottom = (int)(System.Windows.SystemParameters.PrimaryScreenHeight * scale)
        };
    }

    private const uint SPI_GETFOREGROUNDLOCKTIMEOUT = 0x2000;
    private const uint SPI_SETFOREGROUNDLOCKTIMEOUT = 0x2001;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, out uint pvParam, uint fWinIni);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>Holt <paramref name="hWnd"/> zuverlässig in den Vordergrund. Windows blockiert
    /// SetForegroundWindow, wenn der aufrufende Prozess nicht im Vordergrund ist (Vordergrundsperre) –
    /// typisch für Tray-Apps ohne sichtbares Fenster. Die Sperre wird kurz auf 0 gesetzt und danach
    /// wieder auf den alten Wert zurückgestellt (nur im Speicher, nicht persistent).</summary>
    public static void ForceForeground(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return;
        uint timeout = 0;
        bool got = false;
        try
        {
            got = SystemParametersInfo(SPI_GETFOREGROUNDLOCKTIMEOUT, 0, out timeout, 0);
            SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, IntPtr.Zero, 0);
            SetForegroundWindow(hWnd);
        }
        finally
        {
            if (got) SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, new IntPtr(timeout), 0);
        }
    }
}
