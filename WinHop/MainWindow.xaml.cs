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

    private const double PeekWidth = 8; // visible strip when hidden
    private const double LeftMargin = 8;
    private const double TopMargin = 12;

    private readonly ObservableCollection<WindowInfo> _items = new();
    private readonly DispatcherTimer _hideTimer;sdf

    private List<WindowInfo> _allWindows = new();
    private bool _isExpanded;

    public MainWindow()
    {
        InitializeComponent();

        WindowsList.ItemsSource = _items;

        _hideTimer = new DispatcherTimer
        {
            Interval = System.TimeSpan.FromMilliseconds(250),
        };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            if (!IsMouseOver)
                HideToPeek();
        };

        Loaded += (_, _) =>
        {
            // Keep the window alive and parked as a peek strip.
            PositionAndSize();
            Show();
            HideToPeek(immediate: true);
        };

        SourceInitialized += (_, _) => RegisterHotkey();

        Deactivated += (_, _) =>
        {
            // If user clicks elsewhere, collapse (like Arc).
            _hideTimer.Stop();
            _hideTimer.Start();
        };
    }

    private void PositionAndSize()
    {
        var workArea = SystemParameters.WorkArea;

        Top = workArea.Top + TopMargin;
        Height = workArea.Height - (TopMargin * 2);
    }

    private double GetShownLeft()
    {
        var workArea = SystemParameters.WorkArea;
        return workArea.Left + LeftMargin;
    }

    private double GetHiddenLeft()
    {
        var workArea = SystemParameters.WorkArea;
        return workArea.Left - (Width - PeekWidth);
    }

    private void AnimateLeft(double to, bool immediate = false)
    {
        if (immediate)
        {
            BeginAnimation(LeftProperty, null);
            Left = to;
            return;
        }

        var anim = new DoubleAnimation
        {
            To = to,
            Duration = System.TimeSpan.FromMilliseconds(140),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
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

    public void ShowExpanded(bool focusSearch)
    {
        PositionAndSize();
        RefreshWindows();

        _isExpanded = true;
        AnimateLeft(GetShownLeft());

        if (focusSearch)
        {
            ShowActivated = true;
            Activate();
            SearchBox.Focus();
            SearchBox.SelectAll();
        }
        else
        {
            // Don’t steal focus on mere hover.
            ShowActivated = false;
        }
    }

    public void HideToPeek(bool immediate = false)
    {
        _isExpanded = false;
        AnimateLeft(GetHiddenLeft(), immediate);

        // Don’t keep keyboard focus when collapsed
        MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
    }

    public void ToggleHotkey()
    {
        if (_isExpanded)
            HideToPeek();
        else
            ShowExpanded(focusSearch: true);
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
            ToggleHotkey();
        }

        return IntPtr.Zero;
    }

    private void ActivateSelected()
    {
        if (WindowsList.SelectedItem is not WindowInfo wi)
            return;

        HideToPeek();
        Win32.ActivateWindow(wi.Hwnd);
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        ApplyFilter(SearchBox.Text);
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideToPeek();
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

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        if (!_isExpanded)
            ShowExpanded(focusSearch: false);

        _hideTimer.Stop();
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        _hideTimer.Stop();
        _hideTimer.Start();
    }
}