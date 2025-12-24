using System.Collections.Generic;
using System.Linq;

namespace WinHop;

internal sealed class EdgeTriggerManager
{
    private readonly List<EdgeTriggerWindow> _triggers = new();

    public EdgeTriggerManager(MainWindow sidebar)
    {
        foreach (var m in MonitorUtil.GetMonitors())
        {
            var w = new EdgeTriggerWindow(sidebar, m);
            _triggers.Add(w);
            w.Show();
        }
    }

    public bool IsMouseOverAnyTrigger() => _triggers.Any(t => t.IsMouseOver);

    public void RepositionAll()
    {
        foreach (var t in _triggers)
            t.Reposition();
    }
}