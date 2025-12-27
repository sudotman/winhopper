using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WinHop;

internal static class Win32
{
    public const int WM_HOTKEY = 0x0312;

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    public const int GWL_EXSTYLE = -20;
    public const long WS_EX_TOOLWINDOW = 0x00000080L;

    public const int SW_RESTORE = 9;

    // DWM
    private const int DWMWA_CLOAKED = 14;

    internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        out int pvAttribute,
        int cbAttribute
    );

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RegisterHotKey(
        IntPtr hWnd,
        int id,
        uint fsModifiers,
        uint vk
    );

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    internal static string GetWindowTitle(IntPtr hWnd)
    {
        var len = GetWindowTextLength(hWnd);
        if (len <= 0)
            return string.Empty;

        var sb = new StringBuilder(len + 1);
        _ = GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    internal static bool IsCloaked(IntPtr hWnd)
    {
        try
        {
            var hr = DwmGetWindowAttribute(
                hWnd,
                DWMWA_CLOAKED,
                out var cloaked,
                sizeof(int)
            );
            return hr == 0 && cloaked != 0;
        }
        catch
        {
            return false;
        }
    }

    internal static long GetExStyle(IntPtr hWnd)
    {
        return GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64();
    }

    internal static bool IsAltTabCandidate(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return false;

        if (hWnd == GetShellWindow())
            return false;

        if (!IsWindowVisible(hWnd))
            return false;

        if (IsCloaked(hWnd))
            return false;

        var exStyle = GetExStyle(hWnd);
        if ((exStyle & WS_EX_TOOLWINDOW) != 0)
            return false;

        var title = GetWindowTitle(hWnd);
        if (string.IsNullOrWhiteSpace(title))
            return false;

        return true;
    }

    internal static string? TryGetProcessPath(uint pid)
    {
        try
        {
            using var p = Process.GetProcessById((int)pid);
            return p.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    internal static string TryGetProcessName(uint pid)
    {
        try
        {
            using var p = Process.GetProcessById((int)pid);
            return p.ProcessName;
        }
        catch
        {
            return "Unknown";
        }
    }

    internal static void ActivateWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return;

        if (IsIconic(hWnd))
            _ = ShowWindow(hWnd, SW_RESTORE);

        _ = SetForegroundWindow(hWnd);
    }

    private const int WM_CLOSE = 0x0010;

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    internal static void CloseWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return;
        PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("dwmapi.dll")]
    internal static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute
    );

    internal static void TryEnableRoundedCorners(IntPtr hwnd)
    {
        // Windows 11: DWMWA_WINDOW_CORNER_PREFERENCE = 33
        // DWMWCP_ROUND = 2
        try
        {
            int attr = 33;
            int round = 2;
            _ = DwmSetWindowAttribute(hwnd, attr, ref round, sizeof(int));
        }
        catch { }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    internal static (int X, int Y) GetCursorPosition()
    {
        GetCursorPos(out var pt);
        return (pt.X, pt.Y);
    }
}