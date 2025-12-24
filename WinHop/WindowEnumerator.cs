using System.Collections.Generic;
using System.Linq;

namespace WinHop;

internal static class WindowEnumerator
{
    public static List<WindowInfo> GetOpenWindows()
    {
        var result = new List<WindowInfo>();

        Win32.EnumWindows(
            (hWnd, lParam) =>
            {
                if (!Win32.IsAltTabCandidate(hWnd))
                    return true;

                Win32.GetWindowThreadProcessId(hWnd, out var pid);

                var title = Win32.GetWindowTitle(hWnd);
                var procName = Win32.TryGetProcessName(pid);
                var icon = IconExtractor.TryGetIconForWindow(hWnd, pid);

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
}