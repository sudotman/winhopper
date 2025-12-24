using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinHop;

internal static class IconExtractor
{
    private const int WM_GETICON = 0x007F;
    private const int ICON_SMALL = 0;
    private const int ICON_BIG = 1;
    private const int ICON_SMALL2 = 2;

    private const int GCL_HICON = -14;
    private const int GCL_HICONSM = -34;

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_SMALLICON = 0x000000001;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(
        IntPtr hWnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam
    );

    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")]
    private static extern IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr CopyIcon(IntPtr hIcon);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        out SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags
    );

    public static ImageSource? TryGetIconForWindow(IntPtr hwnd, uint pid)
    {
        // 1) Window-provided icon (owned by window) -> CopyIcon so we can destroy safely
        var hIcon = GetWindowIconHandle(hwnd);
        if (hIcon != IntPtr.Zero)
            return FromHiconCopy(hIcon);

        // 2) Fallback: exe icon (owned by us)
        var path = Win32.TryGetProcessPath(pid);
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var owned = GetSmallIconHandleFromPath(path);
        if (owned == IntPtr.Zero)
            return null;

        return FromOwnedHiconAndDestroy(owned);
    }

    private static IntPtr GetWindowIconHandle(IntPtr hwnd)
    {
        var hIcon = SendMessage(hwnd, WM_GETICON, (IntPtr)ICON_SMALL2, IntPtr.Zero);
        if (hIcon != IntPtr.Zero)
            return hIcon;

        hIcon = SendMessage(hwnd, WM_GETICON, (IntPtr)ICON_SMALL, IntPtr.Zero);
        if (hIcon != IntPtr.Zero)
            return hIcon;

        hIcon = SendMessage(hwnd, WM_GETICON, (IntPtr)ICON_BIG, IntPtr.Zero);
        if (hIcon != IntPtr.Zero)
            return hIcon;

        hIcon = GetClassLongPtr(hwnd, GCL_HICONSM);
        if (hIcon != IntPtr.Zero)
            return hIcon;

        return GetClassLongPtr(hwnd, GCL_HICON);
    }

    private static ImageSource? FromHiconCopy(IntPtr hIcon)
    {
        var copy = CopyIcon(hIcon);
        if (copy == IntPtr.Zero)
            return null;

        return FromOwnedHiconAndDestroy(copy);
    }

    private static ImageSource? FromOwnedHiconAndDestroy(IntPtr ownedHIcon)
    {
        try
        {
            var src = Imaging.CreateBitmapSourceFromHIcon(
                ownedHIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(16, 16)
            );
            src.Freeze();
            return src;
        }
        finally
        {
            _ = DestroyIcon(ownedHIcon);
        }
    }

    private static IntPtr GetSmallIconHandleFromPath(string path)
    {
        var res = SHGetFileInfo(
            path,
            0,
            out var info,
            (uint)Marshal.SizeOf<SHFILEINFO>(),
            SHGFI_ICON | SHGFI_SMALLICON
        );

        if (res == IntPtr.Zero || info.hIcon == IntPtr.Zero)
            return IntPtr.Zero;

        // SHGetFileInfo returns an icon handle we own and must DestroyIcon().
        return info.hIcon;
    }
}