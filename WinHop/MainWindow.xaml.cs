using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace WinHop;

public partial class MainWindow : Window
{
    private const int HotkeyId = 1;

    private const double LeftInset = 8;
    private const double TopInset = 12;

    private readonly ObservableCollection<WindowInfo> _items = new();
    private readonly DispatcherTimer _hideTimer;

    private List<WindowInfo> _allWindows = new();
    private bool _animating;

    private MonitorInfo? _currentMonitor;
    private EdgeTriggerManager? _edgeTriggers;

    public bool IsSidebarVisible => IsVisible;

    public MainWindow()
    {
        InitializeComponent();
        WindowsList.ItemsSource = _items;

        _hideTimer = new DispatcherTimer
        {
            Interval = System.TimeSpan.FromMilliseconds(200),
        };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();

            var overSidebar = IsMouseOver;
            var overTrigger = _edgeTriggers?.IsMouseOverAnyTrigger() ?? false;

            if (!overSidebar && !overTrigger)
                HideAnimated();
        };

        Loaded += (_, _) =>
        {
            Hide(); // start hidden
            _currentMonitor = MonitorUtil.GetMonitorFromCursor();
            _edgeTriggers = new EdgeTriggerManager(this);
        };

        SourceInitialized += (_, _) =>
        {
            RegisterHotkey();

            var hwnd = new WindowInteropHelper(this).Handle;
            Win32.TryEnableRoundedCorners(hwnd);
        };

        Deactivated += (_, _) =>
        {
            // If user clicks elsewhere, collapse
            StartHideTimer();
        };
    }

    private void RegisterHotkey()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var src = HwndSource.FromHwnd(hwnd);
        src.AddHook(WndProc);

        Win32.RegisterHotKey(
            hwnd,
            HotkeyId,
            Win32.MOD_CONTROL,
            (uint)KeyInterop.VirtualKeyFromKey(Key.Space)
        );
    }

    protected override void OnClosed(System.EventArgs e)
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            Win32.UnregisterHotKey(hwnd, HotkeyId);
        }
        catch { }

        base.OnClosed(e);
    }

    private IntPtr WndProc(
        IntPtr hwnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled
    )
    {
        if (msg == Win32.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;

            if (IsVisible)
                HideAnimated();
            else
                ShowOnMonitor(MonitorUtil.GetMonitorFromCursor(), focusSearch: true);
        }

        return IntPtr.Zero;
    }

    public void ShowOnMonitor(MonitorInfo monitor, bool focusSearch)
    {
        if (_animating)
            return;

        _currentMonitor = monitor;

        var wa = monitor.WorkAreaPx;
        var dpi = monitor.DpiX;

        var top = MonitorUtil.PxToDip(wa.Top, dpi) + TopInset;
        var height = MonitorUtil.PxToDip(wa.Height, dpi) - (TopInset * 2);

        var shownLeft = MonitorUtil.PxToDip(wa.Left, dpi) + LeftInset;
        var hiddenLeft = shownLeft - Width - 6;

        Top = top;
        Height = height;
        Left = hiddenLeft;

        RefreshWindows();

        ShowActivated = focusSearch;
        Show();
        Topmost = true;

        AnimateLeft(shownLeft);

        if (focusSearch)
        {
            Activate();
            SearchBox.Focus();
            SearchBox.SelectAll();
        }
    }

    private void HideAnimated()
    {
        if (!IsVisible || _animating)
            return;

        if (_currentMonitor is null)
        {
            Hide();
            return;
        }

        var wa = _currentMonitor.WorkAreaPx;
        var dpi = _currentMonitor.DpiX;

        var shownLeft = MonitorUtil.PxToDip(wa.Left, dpi) + LeftInset;
        var hiddenLeft = shownLeft - Width - 6;

        AnimateLeft(hiddenLeft, hideAfter: true);
    }

    private void AnimateLeft(double to, bool hideAfter = false)
    {
        _animating = true;

        var anim = new DoubleAnimation
        {
            To = to,
            Duration = System.TimeSpan.FromMilliseconds(120),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop,
        };

        anim.Completed += (_, _) =>
        {
            BeginAnimation(LeftProperty, null);
            Left = to;

            _animating = false;

            if (hideAfter)
                Hide();
        };

        BeginAnimation(LeftProperty, anim);
    }

    private void RefreshWindows()
    {
        _allWindows = WindowEnumerator.GetOpenWindows();
        ApplyFilter(SearchBox.Text);
    }

    private void ApplyFilter(string query)
    {
        query = (query ?? "").Trim().ToLowerInvariant();

        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allWindows
            : _allWindows
                .Where(w => w.SearchText.ToLowerInvariant().Contains(query))
                .ToList();

        _items.Clear();
        foreach (var w in filtered)
            _items.Add(w);

        WindowsList.SelectedIndex = _items.Count > 0 ? 0 : -1;
    }

    private void StartHideTimer()
    {
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        _hideTimer.Stop();
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        StartHideTimer();
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        ApplyFilter(SearchBox.Text);
    }

    private void ActivateSelected()
    {
        if (WindowsList.SelectedItem is not WindowInfo wi)
            return;

        HideAnimated();
        Win32.ActivateWindow(wi.Hwnd);
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace WinHop;

public partial class MainWindow : Window
{
    private const int HotkeyId = 1;

    private const double LeftInset = 8;
    private const double TopInset = 12;

    private readonly ObservableCollection<WindowInfo> _items = new();
    private readonly DispatcherTimer _hideTimer;

    private List<WindowInfo> _allWindows = new();
    private bool _animating;

    private MonitorInfo _currentMonitor;
    private EdgeTriggerManager? _edgeTriggers;

    public bool IsSidebarVisible => IsVisible;

    public MainWindow()
    {
        InitializeComponent();
        WindowsList.ItemsSource = _items;

        _hideTimer = new DispatcherTimer
        {
            Interval = System.TimeSpan.FromMilliseconds(200),
        };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();

            var overSidebar = IsMouseOver;
            var overTrigger = _edgeTriggers?.IsMouseOverAnyTrigger() ?? false;

            if (!overSidebar && !overTrigger)
                HideAnimated();
        };

        Loaded += (_, _) =>
        {
            Hide(); // start hidden
            _currentMonitor = MonitorUtil.GetMonitorFromCursor();
            _edgeTriggers = new EdgeTriggerManager(this);
        };

        SourceInitialized += (_, _) =>
        {
            RegisterHotkey();

            var hwnd = new WindowInteropHelper(this).Handle;
            Win32.TryEnableRoundedCorners(hwnd);
        };

        Deactivated += (_, _) =>
        {
            // If user clicks elsewhere, collapse
            StartHideTimer();
        };
    }

    private void RegisterHotkey()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var src = HwndSource.FromHwnd(hwnd);
        src.AddHook(WndProc);

        Win32.RegisterHotKey(
            hwnd,
            HotkeyId,
            Win32.MOD_CONTROL,
            (uint)KeyInterop.VirtualKeyFromKey(Key.Space)
        );
    }

    protected override void OnClosed(System.EventArgs e)
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            Win32.UnregisterHotKey(hwnd, HotkeyId);
        }
        catch { }

        base.OnClosed(e);
    }

    private IntPtr WndProc(
        IntPtr hwnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled
    )
    {
        if (msg == Win32.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;

            if (IsVisible)
                HideAnimated();
            else
                ShowOnMonitor(MonitorUtil.GetMonitorFromCursor(), focusSearch: true);
        }

        return IntPtr.Zero;
    }

    public void ShowOnMonitor(MonitorInfo monitor, bool focusSearch)
    {
        if (_animating)
            return;

        _currentMonitor = monitor;

        var wa = monitor.WorkAreaPx;
        var dpi = monitor.DpiX;

        var top = MonitorUtil.PxToDip(wa.Top, dpi) + TopInset;
        var height = MonitorUtil.PxToDip(wa.Height, dpi) - (TopInset * 2);

        var shownLeft = MonitorUtil.PxToDip(wa.Left, dpi) + LeftInset;
        var hiddenLeft = shownLeft - Width - 6;

        Top = top;
        Height = height;
        Left = hiddenLeft;

        RefreshWindows();

        ShowActivated = focusSearch;
        Show();
        Topmost = true;

        AnimateLeft(shownLeft);

        if (focusSearch)
        {
            Activate();
            SearchBox.Focus();
            SearchBox.SelectAll();
        }
    }

    private void HideAnimated()
    {
        if (!IsVisible || _animating)
            return;

        var wa = _currentMonitor.WorkAreaPx;
        var dpi = _currentMonitor.DpiX;

        var shownLeft = MonitorUtil.PxToDip(wa.Left, dpi) + LeftInset;
        var hiddenLeft = shownLeft - Width - 6;

        AnimateLeft(hiddenLeft, hideAfter: true);
    }

    private void AnimateLeft(double to, bool hideAfter = false)
    {
        _animating = true;

        var anim = new DoubleAnimation
        {
            To = to,
            Duration = System.TimeSpan.FromMilliseconds(120),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop,
        };

        anim.Completed += (_, _) =>
        {
            BeginAnimation(LeftProperty, null);
            Left = to;

            _animating = false;

            if (hideAfter)
                Hide();
        };

        BeginAnimation(LeftProperty, anim);
    }

    private void RefreshWindows()
    {
        _allWindows = WindowEnumerator.GetOpenWindows();
        ApplyFilter(SearchBox.Text);
    }

    private void ApplyFilter(string query)
    {
        query = (query ?? "").Trim().ToLowerInvariant();

        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allWindows
            : _allWindows
                .Where(w => w.SearchText.ToLowerInvariant().Contains(query))
                .ToList();

        _items.Clear();
        foreach (var w in filtered)
            _items.Add(w);

        WindowsList.SelectedIndex = _items.Count > 0 ? 0 : -1;
    }

    private void StartHideTimer()
    {
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        _hideTimer.Stop();
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        StartHideTimer();
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        ApplyFilter(SearchBox.Text);
    }

    private void ActivateSelected()
    {
        if (WindowsList.SelectedItem is not WindowInfo wi)
            return;

        HideAnimated();
        Win32.ActivateWindow(wi.Hwnd);
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideAnimated();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            ActivateSelected();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down)
        {
            WindowsList.Focus();
            e.Handled = true;
        }
    }

    private void WindowsList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            SearchBox.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            ActivateSelected();
            e.Handled = true;
        }
    }

    private void WindowsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ActivateSelected();
    }
}
            HideAnimated();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            ActivateSelected();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down)
        {
            WindowsList.Focus();
            e.Handled = true;
        }
    }

    private void WindowsList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            SearchBox.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            ActivateSelected();
            e.Handled = true;
        }
    }

    private void WindowsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ActivateSelected();
    }
}