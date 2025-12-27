using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace WinHop;

internal static class WindowEnumerator
{
    public static List<WindowInfo> GetOpenWindows(bool includeIcons)
    {
        var result = new List<WindowInfo>();
        var selfPid = (uint)Process.GetCurrentProcess().Id;

        Win32.EnumWindows(
            (hWnd, lParam) =>
            {
                if (!Win32.IsAltTabCandidate(hWnd))
                    return true;

                Win32.GetWindowThreadProcessId(hWnd, out var pid);
                if (pid == selfPid)
                    return true;

                var title = Win32.GetWindowTitle(hWnd);
                var procName = Win32.TryGetProcessName(pid);
                var icon = includeIcons ? IconExtractor.TryGetIconForWindow(hWnd, pid) : null;

                result.Add(
                    new WindowInfo
                    {
                        Hwnd = hWnd,
                        Title = title,
                        ProcessName = procName,
                        Icon = icon,
                    }
                );

                return true;
            },
            IntPtr.Zero
        );

        return result
            .OrderBy(w => w.ProcessName)
            .ThenBy(w => w.Title)
            .ToList();
    }

    public static List<WindowInfo> GetOpenWindows()
        => GetOpenWindows(includeIcons: true);
}