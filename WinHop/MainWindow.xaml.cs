using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace WinHop;

public partial class MainWindow : Window
{
    private const int HotkeyId = 1;
    private const double LeftInset = 10;
    private const double TopInset = 10;

    private EdgeTriggerSide _currentSide = EdgeTriggerSide.Right;
    private bool _edgeUiInit;
    private AppSettings _settings = new();
    private int _refreshRequestId;
    private int _iconLoadRequestId;

    private readonly ObservableCollection<WindowInfo> _items = new();
    private readonly ObservableCollection<WindowInfo> _pinnedItems = new();
    private readonly ObservableCollection<WorkspaceViewModel> _workspaces = new();
    private readonly DispatcherTimer _hideTimer;

    private List<WindowInfo> _allWindows = new();
    private Dictionary<IntPtr, ImageSource?> _iconCache = new();
    private bool _animating;
    private IntPtr _lastFocusedHwnd;

    private MonitorInfo? _currentMonitor;
    private EdgeTriggerManager? _edgeTriggers;

    public bool IsSidebarVisible => IsVisible;

    public MainWindow()
    {
        InitializeComponent();
        WindowsList.ItemsSource = _items;
        PinnedApps.ItemsSource = _pinnedItems;
        WorkspacesList.ItemsSource = _workspaces;

        _hideTimer = new DispatcherTimer
        {
            Interval = System.TimeSpan.FromMilliseconds(150),
        };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            if (!IsCursorOverWindow() && !(_edgeTriggers?.IsMouseOverAnyTrigger() ?? false))
                HideAnimated();
        };

        Loaded += (_, _) =>
        {
            Hide();
            _currentMonitor = MonitorUtil.GetMonitorFromCursor();

            _settings = AppSettings.Load();

            _edgeUiInit = true;
            LeftEdgeToggle.IsChecked = _settings.TriggerLeft;
            RightEdgeToggle.IsChecked = _settings.TriggerRight;
            _edgeUiInit = false;

            LoadWorkspaces();
            BuildWorkspaceContextMenu();

            _edgeTriggers = new EdgeTriggerManager(this, _settings.TriggerLeft, _settings.TriggerRight);
        };

        SourceInitialized += (_, _) =>
        {
            RegisterHotkey();
            var hwnd = new WindowInteropHelper(this).Handle;
            Win32.TryEnableRoundedCorners(hwnd);
        };

        Deactivated += (_, _) => StartHideTimer();
    }

    private void LoadWorkspaces()
    {
        _workspaces.Clear();
        foreach (var ws in _settings.Workspaces)
        {
            _workspaces.Add(new WorkspaceViewModel
            {
                Id = ws.Id,
                Name = ws.Name,
                IsExpanded = ws.IsExpanded,
                // IMPORTANT: Create a new list, not a reference to the original!
                ProcessNames = new List<string>(ws.ProcessNames),
            });
        }
    }

    private void BuildWorkspaceContextMenu()
    {
        AddToWorkspaceMenu.Items.Clear();
        foreach (var ws in _workspaces)
        {
            var mi = new MenuItem { Header = ws.Name, Tag = ws };
            mi.Click += MenuItem_AddToWorkspace;
            AddToWorkspaceMenu.Items.Add(mi);
        }

        if (_workspaces.Count > 0)
            AddToWorkspaceMenu.Items.Add(new Separator());

        var newWs = new MenuItem { Header = "+ New workspace" };
        newWs.Click += MenuItem_NewWorkspace;
        AddToWorkspaceMenu.Items.Add(newWs);
    }

    private void RegisterHotkey()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var src = HwndSource.FromHwnd(hwnd);
        src.AddHook(WndProc);

        Win32.RegisterHotKey(hwnd, HotkeyId, Win32.MOD_CONTROL,
            (uint)KeyInterop.VirtualKeyFromKey(Key.Space));
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

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            if (IsVisible)
                HideAnimated();
            else
                ShowOnMonitor(MonitorUtil.GetMonitorFromCursor(), GetPreferredSideForHotkey(), focusSearch: true);
        }
        return IntPtr.Zero;
    }

    public void ShowOnMonitor(MonitorInfo monitor, bool focusSearch)
        => ShowOnMonitor(monitor, GetPreferredSideForHotkey(), focusSearch);

    public void ShowOnMonitor(MonitorInfo monitor, EdgeTriggerSide side, bool focusSearch)
    {
        if (_animating) return;

        // Capture the currently focused window BEFORE we take focus
        _lastFocusedHwnd = Win32.GetForegroundWindow();

        _currentMonitor = monitor;
        _currentSide = side;

        var wa = monitor.WorkAreaPx;
        var dpi = monitor.DpiX;

        var top = MonitorUtil.PxToDip(wa.Top, dpi) + TopInset;
        var height = MonitorUtil.PxToDip(wa.Height, dpi) - (TopInset * 2);
        var leftEdge = MonitorUtil.PxToDip(wa.Left, dpi);
        var rightEdge = MonitorUtil.PxToDip(wa.Right, dpi);

        var shownLeft = side == EdgeTriggerSide.Left
            ? leftEdge + LeftInset
            : rightEdge - Width - LeftInset;

        Top = top;
        Height = height;
        Left = shownLeft;

        Root.Opacity = 0;
        SlideTransform.X = side == EdgeTriggerSide.Left ? -Width : Width;

        ShowActivated = focusSearch;
        Show();
        Topmost = true;

        var reqId = ++_refreshRequestId;
        AnimateIn(() =>
        {
            if (reqId != _refreshRequestId) return;
            RefreshWindowsFastThenIcons();
        });

        if (focusSearch)
        {
            Activate();
            SearchBox.Focus();
            SearchBox.SelectAll();
        }
    }

    private void HideAnimated()
    {
        if (!IsVisible || _animating) return;
        if (_currentMonitor is null) { Hide(); return; }
        AnimateOut();
    }

    private void AnimateIn(System.Action? afterShown)
    {
        _animating = true;
        var duration = System.TimeSpan.FromMilliseconds(160);
        var easing = new ExponentialEase { Exponent = 4, EasingMode = EasingMode.EaseOut };

        var slide = new DoubleAnimation { To = 0, Duration = duration, EasingFunction = easing };
        var fade = new DoubleAnimation
        {
            To = 1,
            Duration = System.TimeSpan.FromMilliseconds(100),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        slide.Completed += (_, _) =>
        {
            SlideTransform.X = 0;
            Root.Opacity = 1;
            SlideTransform.BeginAnimation(TranslateTransform.XProperty, null);
            Root.BeginAnimation(OpacityProperty, null);
            _animating = false;
            afterShown?.Invoke();
        };

        SlideTransform.BeginAnimation(TranslateTransform.XProperty, slide);
        Root.BeginAnimation(OpacityProperty, fade);
    }

    private void AnimateOut()
    {
        _animating = true;
        var toX = _currentSide == EdgeTriggerSide.Left ? -Width : Width;
        var duration = System.TimeSpan.FromMilliseconds(120);
        var easing = new ExponentialEase { Exponent = 3, EasingMode = EasingMode.EaseIn };

        var slide = new DoubleAnimation { To = toX, Duration = duration, EasingFunction = easing };
        var fade = new DoubleAnimation
        {
            To = 0,
            Duration = System.TimeSpan.FromMilliseconds(80),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };

        slide.Completed += (_, _) =>
        {
            SlideTransform.X = toX;
            Root.Opacity = 0;
            SlideTransform.BeginAnimation(TranslateTransform.XProperty, null);
            Root.BeginAnimation(OpacityProperty, null);
            _animating = false;
            Hide();
        };

        SlideTransform.BeginAnimation(TranslateTransform.XProperty, slide);
        Root.BeginAnimation(OpacityProperty, fade);
    }

    private void RefreshWindowsFastThenIcons()
    {
        var rawWindows = WindowEnumerator.GetOpenWindows(includeIcons: false);
        _allWindows = new List<WindowInfo>();

        // Apply cached icons and mark the focused window (but don't reorder)
        foreach (var w in rawWindows)
        {
            var isFocused = w.Hwnd == _lastFocusedHwnd;
            _iconCache.TryGetValue(w.Hwnd, out var cachedIcon);

            _allWindows.Add(new WindowInfo
            {
                Hwnd = w.Hwnd,
                Title = w.Title,
                ProcessName = w.ProcessName,
                Icon = cachedIcon,
                IsFocused = isFocused,
            });
        }

        UpdatePinnedApps();
        UpdateWorkspaceWindows();
        ApplyFilter(SearchBox.Text);
        StartIncrementalIconLoad();
    }

    private void UpdatePinnedApps()
    {
        _pinnedItems.Clear();
        foreach (var procName in _settings.PinnedProcessNames)
        {
            var win = _allWindows.FirstOrDefault(w =>
                w.ProcessName.Equals(procName, System.StringComparison.OrdinalIgnoreCase));
            if (win is not null)
                _pinnedItems.Add(win);
        }
    }

    private void UpdateWorkspaceWindows()
    {
        foreach (var ws in _workspaces)
        {
            ws.Windows.Clear();
            foreach (var procName in ws.ProcessNames)
            {
                var wins = _allWindows.Where(w =>
                    w.ProcessName.Equals(procName, System.StringComparison.OrdinalIgnoreCase));
                foreach (var win in wins)
                    ws.Windows.Add(win);
            }
        }
    }

    private void StartIncrementalIconLoad()
    {
        var reqId = ++_iconLoadRequestId;
        _ = Dispatcher.InvokeAsync(() => LoadNextIconBatch(reqId), DispatcherPriority.Background);
    }

    private void LoadNextIconBatch(int reqId)
    {
        if (reqId != _iconLoadRequestId || !IsVisible) return;

        var processed = 0;
        for (var i = 0; i < _allWindows.Count && processed < 5; i++)
        {
            var wi = _allWindows[i];
            if (wi.Icon is not null) continue;

            Win32.GetWindowThreadProcessId(wi.Hwnd, out var pid);
            var icon = IconExtractor.TryGetIconForWindow(wi.Hwnd, pid);
            _iconCache[wi.Hwnd] = icon;

            var updated = new WindowInfo
            {
                Hwnd = wi.Hwnd,
                Title = wi.Title,
                ProcessName = wi.ProcessName,
                Icon = icon,
                IsFocused = wi.IsFocused,
            };
            _allWindows[i] = updated;

            // Update in _items
            for (var j = 0; j < _items.Count; j++)
            {
                if (_items[j].Hwnd == wi.Hwnd)
                {
                    _items[j] = updated;
                    break;
                }
            }

            // Update in pinned
            for (var j = 0; j < _pinnedItems.Count; j++)
            {
                if (_pinnedItems[j].Hwnd == wi.Hwnd)
                {
                    _pinnedItems[j] = updated;
                    break;
                }
            }

            // Update in workspaces
            foreach (var ws in _workspaces)
            {
                for (var j = 0; j < ws.Windows.Count; j++)
                {
                    if (ws.Windows[j].Hwnd == wi.Hwnd)
                    {
                        ws.Windows[j] = updated;
                        break;
                    }
                }
            }

            processed++;
        }

        if (_allWindows.Any(w => w.Icon is null))
            _ = Dispatcher.InvokeAsync(() => LoadNextIconBatch(reqId), DispatcherPriority.Background);
    }

    private void ApplyFilter(string query)
    {
        query = (query ?? "").Trim().ToLowerInvariant();

        // Get all process names that are in workspaces
        var workspaceProcesses = new HashSet<string>(
            _workspaces.SelectMany(ws => ws.ProcessNames),
            System.StringComparer.OrdinalIgnoreCase);

        // Filter windows: exclude those in workspaces (unless focused or searching)
        var filtered = _allWindows.Where(w =>
        {
            // When searching, show all matches
            if (!string.IsNullOrWhiteSpace(query))
                return w.SearchText.ToLowerInvariant().Contains(query);
            
            // Hide windows that are in workspaces (they stay in their workspace)
            return !workspaceProcesses.Contains(w.ProcessName);
        }).ToList();

        _items.Clear();
        foreach (var w in filtered)
            _items.Add(w);

        // Only auto-select when searching (user is actively filtering)
        if (!string.IsNullOrWhiteSpace(query) && _items.Count > 0)
            WindowsList.SelectedIndex = 0;
    }

    private void StartHideTimer()
    {
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        _hideTimer.Stop();
        if (!IsActive)
        {
            Activate();
            SearchBox.Focus();
        }
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e) => StartHideTimer();

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        // When window loses focus, check if cursor is still over us
        if (!IsCursorOverWindow())
            HideAnimated();
    }

    private bool IsCursorOverWindow()
    {
        try
        {
            var cursorPos = Win32.GetCursorPosition();
            var windowRect = new Rect(Left, Top, ActualWidth, ActualHeight);
            return windowRect.Contains(new Point(cursorPos.X, cursorPos.Y));
        }
        catch { return IsMouseOver; }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { HideAnimated(); e.Handled = true; return; }

        // Ctrl+K to focus search
        if (e.Key == Key.K && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Tab)
        {
            if (SearchBox.IsFocused)
            {
                WindowsList.Focus();
                if (WindowsList.SelectedIndex < 0 && _items.Count > 0)
                    WindowsList.SelectedIndex = 0;
            }
            else
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
            }
            e.Handled = true;
            return;
        }

        if (e.Key >= Key.D1 && e.Key <= Key.D9)
        {
            var index = e.Key - Key.D1;
            if (index < _items.Count)
            {
                WindowsList.SelectedIndex = index;
                ActivateSelected();
            }
            e.Handled = true;
            return;
        }

        if (SearchBox.IsFocused)
        {
            if (e.Key == Key.Down)
            {
                WindowsList.Focus();
                if (WindowsList.SelectedIndex < 0 && _items.Count > 0)
                    WindowsList.SelectedIndex = 0;
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                WindowsList.Focus();
                WindowsList.SelectedIndex = _items.Count > 0 ? _items.Count - 1 : -1;
                e.Handled = true;
            }
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter(SearchBox.Text);

    private void ActivateSelected()
    {
        if (WindowsList.SelectedItem is not WindowInfo wi) return;
        HideAnimated();
        Win32.ActivateWindow(wi.Hwnd);
    }

    private void ActivateWindow(WindowInfo wi)
    {
        HideAnimated();
        Win32.ActivateWindow(wi.Hwnd);
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { ActivateSelected(); e.Handled = true; }
    }

    private void WindowsList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { ActivateSelected(); e.Handled = true; }
        else if (e.Key == Key.Up && WindowsList.SelectedIndex == 0)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
    }

    private void WindowsList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (WindowsList.SelectedItem is WindowInfo) ActivateSelected();
    }

    private void WindowsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WindowsList.SelectedItem is not null)
            WindowsList.ScrollIntoView(WindowsList.SelectedItem);
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not WindowInfo wi) return;
        
        Win32.CloseWindow(wi.Hwnd);
        _items.Remove(wi);
        _allWindows.Remove(wi);
        e.Handled = true;
    }

    private void WindowItem_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle)
        {
            if (sender is not FrameworkElement fe) return;
            if (fe.DataContext is not WindowInfo wi) return;
            
            Win32.CloseWindow(wi.Hwnd);
            _items.Remove(wi);
            _allWindows.Remove(wi);
            e.Handled = true;
        }
    }

    private void PinnedApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is WindowInfo wi)
            ActivateWindow(wi);
    }

    private void WorkspaceHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton tb && tb.Tag is WorkspaceViewModel ws)
        {
            // Save expansion state
            var settingsWs = _settings.Workspaces.FirstOrDefault(w => w.Id == ws.Id);
            if (settingsWs is not null)
            {
                settingsWs.IsExpanded = ws.IsExpanded;
                _settings.Save();
            }
        }
    }

    private void WorkspaceWindow_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is WindowInfo wi)
            ActivateWindow(wi);
    }

    private void WorkspaceWindow_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle)
        {
            if (sender is not FrameworkElement fe) return;
            if (fe.DataContext is not WindowInfo wi) return;
            
            Win32.CloseWindow(wi.Hwnd);
            
            // Remove from workspace UI
            foreach (var ws in _workspaces)
                ws.Windows.Remove(wi);
            
            _allWindows.Remove(wi);
            e.Handled = true;
        }
    }

    private void MenuItem_PinToTop(object sender, RoutedEventArgs e)
    {
        if (WindowsList.SelectedItem is not WindowInfo wi) return;

        if (!_settings.PinnedProcessNames.Contains(wi.ProcessName, System.StringComparer.OrdinalIgnoreCase))
        {
            _settings.PinnedProcessNames.Add(wi.ProcessName);
            _settings.Save();
            UpdatePinnedApps();
        }
    }

    private void MenuItem_Unpin(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi) return;
        var cm = mi.Parent as ContextMenu;
        if (cm?.PlacementTarget is not Button btn) return;
        if (btn.DataContext is not WindowInfo wi) return;

        _settings.PinnedProcessNames.RemoveAll(p => 
            p.Equals(wi.ProcessName, System.StringComparison.OrdinalIgnoreCase));
        _settings.Save();
        _pinnedItems.Remove(wi);
    }

    private void MenuItem_AddToWorkspace(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi || mi.Tag is not WorkspaceViewModel ws) return;
        if (WindowsList.SelectedItem is not WindowInfo wi) return;

        var settingsWs = _settings.Workspaces.FirstOrDefault(w => w.Id == ws.Id);
        if (settingsWs is null) return;

        if (!settingsWs.ProcessNames.Contains(wi.ProcessName, System.StringComparer.OrdinalIgnoreCase))
        {
            settingsWs.ProcessNames.Add(wi.ProcessName);
            ws.ProcessNames.Add(wi.ProcessName);
            _settings.Save();
            UpdateWorkspaceWindows();
        }
    }

    private void WorkspaceWindow_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not WindowInfo wi) return;
        
        // Find the parent workspace
        var parent = fe;
        while (parent != null)
        {
            if (parent is ItemsControl ic && ic.Tag is WorkspaceViewModel ws)
            {
                // Set the context menu Tag with both workspace and window
                if (fe.ContextMenu is ContextMenu cm && cm.Items[0] is MenuItem mi)
                {
                    mi.Tag = (ws, wi);
                }
                break;
            }
            parent = VisualTreeHelper.GetParent(parent) as FrameworkElement;
        }
    }

    private void MenuItem_RemoveFromWorkspace(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi) return;
        if (mi.Tag is not ValueTuple<WorkspaceViewModel, WindowInfo> tuple) return;
        
        var (ws, wi) = tuple;

        // Remove by process name from the workspace
        var settingsWs = _settings.Workspaces.FirstOrDefault(w => w.Id == ws.Id);
        if (settingsWs is not null)
        {
            settingsWs.ProcessNames.RemoveAll(p => 
                p.Equals(wi.ProcessName, System.StringComparison.OrdinalIgnoreCase));
            ws.ProcessNames.RemoveAll(p => 
                p.Equals(wi.ProcessName, System.StringComparison.OrdinalIgnoreCase));
            _settings.Save();
        }

        // Remove all windows with this process name from the workspace UI
        var toRemove = ws.Windows.Where(w => 
            w.ProcessName.Equals(wi.ProcessName, System.StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var w in toRemove)
            ws.Windows.Remove(w);
    }

    private void MenuItem_NewWorkspace(object sender, RoutedEventArgs e)
    {
        var name = $"Workspace {_settings.Workspaces.Count + 1}";
        var newWs = new Workspace { Name = name };
        _settings.Workspaces.Add(newWs);
        _settings.Save();

        _workspaces.Add(new WorkspaceViewModel
        {
            Id = newWs.Id,
            Name = newWs.Name,
            IsExpanded = newWs.IsExpanded,
            ProcessNames = newWs.ProcessNames,
        });

        BuildWorkspaceContextMenu();
    }

    private void MenuItem_RenameWorkspace(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi) return;
        var cm = mi.Parent as ContextMenu;
        if (cm?.PlacementTarget is not ToggleButton tb) return;
        if (tb.Tag is not WorkspaceViewModel ws) return;

        // Create a simple input dialog inline
        var dialog = new Window
        {
            Title = "Rename Workspace",
            Width = 300,
            Height = 120,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            WindowStyle = WindowStyle.ToolWindow,
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
        };

        var sp = new StackPanel { Margin = new Thickness(15) };
        var textBox = new TextBox 
        { 
            Text = ws.Name, 
            FontSize = 14,
            Padding = new Thickness(8, 6, 8, 6),
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
        };
        textBox.SelectAll();
        
        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var okBtn = new Button { Content = "Save", Width = 70, Padding = new Thickness(0, 6, 0, 6), IsDefault = true };
        var cancelBtn = new Button { Content = "Cancel", Width = 70, Padding = new Thickness(0, 6, 0, 6), Margin = new Thickness(8, 0, 0, 0), IsCancel = true };
        
        okBtn.Click += (_, _) => { dialog.DialogResult = true; dialog.Close(); };
        cancelBtn.Click += (_, _) => dialog.Close();
        
        btnPanel.Children.Add(okBtn);
        btnPanel.Children.Add(cancelBtn);
        sp.Children.Add(textBox);
        sp.Children.Add(btnPanel);
        dialog.Content = sp;
        
        textBox.Focus();
        
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            ws.Name = textBox.Text.Trim();
            var settingsWs = _settings.Workspaces.FirstOrDefault(w => w.Id == ws.Id);
            if (settingsWs is not null)
            {
                settingsWs.Name = ws.Name;
                _settings.Save();
            }
            BuildWorkspaceContextMenu();
            
            // Force UI refresh
            var idx = _workspaces.IndexOf(ws);
            if (idx >= 0)
            {
                _workspaces.RemoveAt(idx);
                _workspaces.Insert(idx, ws);
            }
        }
    }

    private void MenuItem_DeleteWorkspace(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi) return;
        var cm = mi.Parent as ContextMenu;
        if (cm?.PlacementTarget is not ToggleButton tb) return;
        if (tb.Tag is not WorkspaceViewModel ws) return;

        // Remove from settings
        var settingsWs = _settings.Workspaces.FirstOrDefault(w => w.Id == ws.Id);
        if (settingsWs is not null)
        {
            _settings.Workspaces.Remove(settingsWs);
            _settings.Save();
        }

        // Remove from UI
        _workspaces.Remove(ws);
        BuildWorkspaceContextMenu();
        
        // Refresh main list since those windows should now appear there
        ApplyFilter(SearchBox.Text);
    }

    private EdgeTriggerSide GetPreferredSideForHotkey()
    {
        var left = LeftEdgeToggle.IsChecked == true;
        var right = RightEdgeToggle.IsChecked == true;
        if (left && !right) return EdgeTriggerSide.Left;
        if (right && !left) return EdgeTriggerSide.Right;
        return _currentSide;
    }

    private void EdgeToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_edgeUiInit) return;

        var left = LeftEdgeToggle.IsChecked == true;
        var right = RightEdgeToggle.IsChecked == true;

        _settings.TriggerLeft = left;
        _settings.TriggerRight = right;
        _settings.Save();

        _edgeTriggers?.UpdateSides(left, right);
    }
}

public class WorkspaceViewModel : INotifyPropertyChanged
{
    private bool _isExpanded = true;
    private string _name = "";

    public string Id { get; set; } = "";
    
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }
    }
    
    public List<string> ProcessNames { get; set; } = new();
    public ObservableCollection<WindowInfo> Windows { get; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
