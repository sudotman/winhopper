using System.Windows.Media;

namespace WinHop;

public sealed class WindowInfo
{
    public required IntPtr Hwnd { get; init; }
    public required string Title { get; init; }
    public required string ProcessName { get; init; }
    public ImageSource? Icon { get; init; }
    public bool IsFocused { get; init; }

    public string SearchText => $"{Title} {ProcessName}";
}