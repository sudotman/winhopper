using System.Collections.Generic;
using System.Linq;

namespace WinHop;

internal sealed class EdgeTriggerManager
{
    private readonly List<EdgeTriggerWindow> _triggers = new();
    private readonly MainWindow _sidebar;

    public EdgeTriggerManager(MainWindow sidebar, bool leftEnabled, bool rightEnabled)
    {
        _sidebar = sidebar;
        Rebuild(leftEnabled, rightEnabled);
    }

    public bool IsMouseOverAnyTrigger() => _triggers.Any(t => t.IsMouseOver);

    public void RepositionAll()
    {
        foreach (var t in _triggers)
            t.Reposition();
    }

    public void UpdateSides(bool leftEnabled, bool rightEnabled) => Rebuild(leftEnabled, rightEnabled);

    private void Rebuild(bool leftEnabled, bool rightEnabled)
    {
        foreach (var t in _triggers)
        {
            try { t.Close(); } catch { }
        }
        _triggers.Clear();

        foreach (var m in MonitorUtil.GetMonitors())
        {
            if (leftEnabled)
                Create(m, EdgeTriggerSide.Left);
            if (rightEnabled)
                Create(m, EdgeTriggerSide.Right);
        }
    }

    private void Create(MonitorInfo monitor, EdgeTriggerSide side)
    {
        var w = new EdgeTriggerWindow(_sidebar, monitor, side);
        _triggers.Add(w);
        w.Show();
    }
}