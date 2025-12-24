using System.Windows;

namespace WinHop;

public partial class EdgeTriggerWindow : Window
{
    private readonly MainWindow _sidebar;
    public MonitorInfo Monitor { get; }

    public EdgeTriggerWindow(MainWindow sidebar, MonitorInfo monitor)
    {
        InitializeComponent();
        _sidebar = sidebar;
        Monitor = monitor;

        Loaded += (_, _) => Reposition();
        MouseEnter += (_, _) =>
        {
            if (!_sidebar.IsSidebarVisible)
                _sidebar.ShowOnMonitor(Monitor, focusSearch: false);
        };
    }

    public void Reposition()
    {
        var wa = Monitor.WorkAreaPx;
        var dpi = Monitor.DpiX;

        Left = MonitorUtil.PxToDip(wa.Left, dpi);
        Top = MonitorUtil.PxToDip(wa.Top, dpi);
        Height = MonitorUtil.PxToDip(wa.Height, dpi);
    }
}