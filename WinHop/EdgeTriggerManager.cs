using System.Collections.Generic;
using System.Linq;

namespace WinHop;

internal sealed class EdgeTriggerManager
{
    private readonly List<EdgeTriggerWindow> _triggers = new();
    private readonly MainWindow _sidebar;
    private bool _leftEnabled;
    private bool _rightEnabled;
    private int _triggerWidth;

    public EdgeTriggerManager(MainWindow sidebar, bool leftEnabled, bool rightEnabled, int triggerWidth)
    {
        _sidebar = sidebar;
        _triggerWidth = triggerWidth;
        UpdateSides(leftEnabled, rightEnabled);
    }

    public bool IsMouseOverAnyTrigger() => _triggers.Any(t => t.IsMouseOver);

    public void RepositionAll()
    {
        foreach (var t in _triggers)
            t.Reposition();
    }

    public void UpdateSides(bool leftEnabled, bool rightEnabled)
    {
        _leftEnabled = leftEnabled;
        _rightEnabled = rightEnabled;
        RebuildTriggers();
    }

    public void UpdateWidth(int width)
    {
        _triggerWidth = width;
        RebuildTriggers();
    }

    private void RebuildTriggers()
    {
        foreach (var t in _triggers.ToList())
        {
            try { t.Close(); } catch { }
        }
        _triggers.Clear();

        foreach (var m in MonitorUtil.GetMonitors())
        {
            if (_leftEnabled)
                CreateTrigger(m, EdgeTriggerSide.Left);
            if (_rightEnabled)
                CreateTrigger(m, EdgeTriggerSide.Right);
        }
    }

    private void CreateTrigger(MonitorInfo monitor, EdgeTriggerSide side)
    {
        var w = new EdgeTriggerWindow(_sidebar, monitor, side, _triggerWidth);
        _triggers.Add(w);
        w.Show();
    }
}