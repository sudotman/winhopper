using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WinHop;

public class FocusedToBrushConverter : IValueConverter
{
    // Focused: bright cyan accent
    private static readonly SolidColorBrush FocusedDot = new(Color.FromRgb(90, 180, 255));
    private static readonly SolidColorBrush UnfocusedDot = new(Colors.Transparent);
    // Focused text is bright white, unfocused is more muted
    private static readonly SolidColorBrush FocusedText = new(Color.FromRgb(255, 255, 255));
    private static readonly SolidColorBrush UnfocusedText = new(Color.FromArgb(180, 255, 255, 255));

    static FocusedToBrushConverter()
    {
        FocusedDot.Freeze();
        UnfocusedDot.Freeze();
        FocusedText.Freeze();
        UnfocusedText.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isFocused = value is bool b && b;
        var isText = parameter is string s && s == "text";

        if (isText)
            return isFocused ? FocusedText : UnfocusedText;

        return isFocused ? FocusedDot : UnfocusedDot;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

