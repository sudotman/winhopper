using System.Windows;

namespace WinHop;

public partial class EdgeTriggerWindow : Window
{
    private readonly MainWindow _sidebar;
    public MonitorInfo Monitor { get; }
    public EdgeTriggerSide Side { get; }
    public int TriggerWidth { get; }

    public EdgeTriggerWindow(MainWindow sidebar, MonitorInfo monitor, EdgeTriggerSide side, int triggerWidth)
    {
        InitializeComponent();
        _sidebar = sidebar;
        Monitor = monitor;
        Side = side;
        TriggerWidth = triggerWidth;
        Width = triggerWidth;

        Loaded += (_, _) => Reposition();
        MouseEnter += (_, _) =>
        {
            if (!_sidebar.IsSidebarVisible)
                _sidebar.ShowOnMonitor(Monitor, Side, focusSearch: false);
        };
    }

    public void Reposition()
    {
        var wa = Monitor.WorkAreaPx;
        var dpi = Monitor.DpiX;

        var leftEdge = MonitorUtil.PxToDip(wa.Left, dpi);
        var rightEdge = MonitorUtil.PxToDip(wa.Right, dpi);

        Left = Side == EdgeTriggerSide.Left
            ? leftEdge
            : rightEdge - Width;

        Top = MonitorUtil.PxToDip(wa.Top, dpi);
        Height = MonitorUtil.PxToDip(wa.Height, dpi);
    }
}