using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace WinHop;

public partial class MainWindow : Window
{
    private const int HotkeyId = 1;

    private readonly ObservableCollection<WindowInfo> _items = new();
    private readonly DispatcherTimer _hideTimer;

    private EdgeTriggerWindow? _edge;

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
            if (!IsMouseOver && !(_edge?.IsMouseOver ?? false))
                HideSidebar();
        };

        Loaded += (_, _) => Hide();
        SourceInitialized += (_, _) =>
        {
            RegisterHotkey();
            EnsureEdgeTrigger();
        };

        Deactivated += (_, _) =>
        {
            // If user alt-tabs away, hide quickly (Arc-ish feel)
            _hideTimer.Stop();
            _hideTimer.Start();
        };

        MouseLeave += (_, _) =>
        {
            _hideTimer.Stop();
            _hideTimer.Start();
        };
    }

    private void EnsureEdgeTrigger()
    {
        if (_edge is not null)
            return;

        _edge = new EdgeTriggerWindow(this);
        _edge.Show();
    }

    private void RegisterHotkey()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var src = HwndSource.FromHwnd(hwnd);
        src.AddHook(WndProc);

        // Ctrl+Space
        var ok = Win32.RegisterHotKey(
            hwnd,
            HotkeyId,
            Win32.MOD_CONTROL,
            (uint)KeyInterop.VirtualKeyFromKey(Key.Space)
        );

        if (!ok)
        {
            // If it fails, you can change combo or show a toast later.
        }
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
            ToggleSidebar();
        }

        return IntPtr.Zero;
    }

    public void ToggleSidebar()
    {
        if (IsVisible)
            HideSidebar();
        else
            ShowSidebar();
    }

    public void ShowSidebar()
    {
        RefreshWindows();

        var workArea = SystemParameters.WorkArea;

        Top = workArea.Top + 12;
        Height = workArea.Height - 24;

        // Slide in from left
        Left = workArea.Left + 8;

        Show();
        Activate();

        SearchBox.Text = "";
        SearchBox.Focus();
        WindowsList.SelectedIndex = _items.Count > 0 ? 0 : -1;
        WindowsList.ScrollIntoView(WindowsList.SelectedItem);
    }

    public void HideSidebar()
    {
        Hide();
    }

    private void RefreshWindows()
    {
        var windows = WindowEnumerator.GetOpenWindows();

        _items.Clear();
        foreach (var w in windows)
            _items.Add(w);

        ApplyFilter(SearchBox.Text);
    }

    private void ApplyFilter(string query)
    {
        query = (query ?? "").Trim().ToLowerInvariant();

        var all = WindowEnumerator.GetOpenWindows();

        var filtered = string.IsNullOrWhiteSpace(query)
            ? all
            : all.Where(w => w.SearchText.ToLowerInvariant().Contains(query)).ToList();

        _items.Clear();
        foreach (var w in filtered)
            _items.Add(w);

        WindowsList.SelectedIndex = _items.Count > 0 ? 0 : -1;
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        ApplyFilter(SearchBox.Text);
    }

    private void ActivateSelected()
    {
        if (WindowsList.SelectedItem is not WindowInfo wi)
            return;

        HideSidebar();
        Win32.ActivateWindow(wi.Hwnd);
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideSidebar();
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