using System.Windows;

namespace WinHop;

public partial class EdgeTriggerWindow : Window
{
    private readonly MainWindow _sidebar;

    public EdgeTriggerWindow(MainWindow sidebar)
    {
        InitializeComponent();
        _sidebar = sidebar;

        Loaded += (_, _) => Reposition();
        MouseEnter += (_, _) => _sidebar.ShowSidebar();
    }

    private void Reposition()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left;
        Top = workArea.Top;
        Height = workArea.Height;

        // Important: fully transparent sometimes won’t get hit-testing.
        // Use very low opacity to reliably receive MouseEnter.
        Opacity = 0.01;
    }
}