using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace WinHop;

public sealed record MonitorInfo(
    IntPtr Handle,
    RectPx WorkAreaPx,
    uint DpiX
);

public readonly record struct RectPx(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

internal static class MonitorUtil
{
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    internal static IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var list = new List<MonitorInfo>();

        _ = EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            (IntPtr hMon, IntPtr hdc, ref RECT rc, IntPtr data) =>
            {
                var mi = new MONITORINFOEX();
                mi.cbSize = Marshal.SizeOf<MONITORINFOEX>();

                if (!GetMonitorInfo(hMon, ref mi))
                    return true;

                var work = new RectPx(
                    mi.rcWork.Left,
                    mi.rcWork.Top,
                    mi.rcWork.Right,
                    mi.rcWork.Bottom
                );

                var dpiX = TryGetDpiX(hMon);
                list.Add(new MonitorInfo(hMon, work, dpiX));

                return true;
            },
            IntPtr.Zero
        );

        return list;
    }

    internal static MonitorInfo GetMonitorFromCursor()
    {
        GetCursorPos(out var pt);
        var hMon = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);

        foreach (var m in GetMonitors())
        {
            if (m.Handle == hMon)
                return m;
        }

        // fallback
        var all = GetMonitors();
        return all.Count > 0 ? all[0] : new MonitorInfo(IntPtr.Zero, new RectPx(0, 0, 0, 0), 96);
    }

    internal static double PxToDip(int px, uint dpiX)
    {
        if (dpiX == 0)
            dpiX = 96;
        return px * 96.0 / dpiX;
    }

    private static uint TryGetDpiX(IntPtr hMon)
    {
        try
        {
            var hr = GetDpiForMonitor(hMon, MonitorDpiType.EffectiveDpi, out var x, out _);
            if (hr == 0 && x != 0)
                return x;
        }
        catch { }

        return 96;
    }

    // Win32

    private delegate bool MonitorEnumProc(
        IntPtr hMonitor,
        IntPtr hdcMonitor,
        ref RECT lprcMonitor,
        IntPtr dwData
    );

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr lprcClip,
        MonitorEnumProc lpfnEnum,
        IntPtr dwData
    );

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(
        IntPtr hmonitor,
        MonitorDpiType dpiType,
        out uint dpiX,
        out uint dpiY
    );

    private enum MonitorDpiType
    {
        EffectiveDpi = 0,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }
}