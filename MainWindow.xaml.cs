using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ParadoxusBrowser.Data.Models;
using ParadoxusBrowser.ViewModels;
using System.Windows.Documents;

namespace ParadoxusBrowser
{
    public partial class MainWindow : Window
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool ReleaseCapture();

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_RESTORE = 0xF120;

        public static readonly IValueConverter BoolToVisibilityConverter = new BooleanToVisibilityConverter();
        public static readonly IValueConverter InverseBoolToVisibilityConverter = new InverseBooleanToVisibilityConverter();
        public static readonly IMultiValueConverter TabMatchConverter = new EqualityMultiConverter();

        private Point _dragStartPoint;
        private TabViewModel? _draggedTab;
        private FrameworkElement? _draggedElement;
        private TabDragAdorner? _dragAdorner;
        private Point _adornerOffset;
        private bool _isDraggingTab;
        private int _sourceIndex = -1;
        private int _targetIndex = -1;

        private bool _isFullscreen;
        private WindowState _preFullscreenState = WindowState.Normal;

        public MainWindow() : this(shouldRestoreSession: true)
        {
        }

        public MainWindow(bool shouldRestoreSession)
        {
            InitializeComponent();
            DataContext = new MainViewModel(shouldRestoreSession);
            Loaded += MainWindow_Loaded;
            KeyDown += MainWindow_KeyDown;
            Closing += MainWindow_Closing;
            PreviewMouseWheel += MainWindow_PreviewMouseWheel;
            PreviewMouseDown += MainWindow_PreviewMouseDown;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RestoreWindowState();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveWindowState();
            if (DataContext is MainViewModel vm)
            {
                vm.SaveSession();
                if (ParadoxusBrowser.Core.SettingsManager.Instance.ClearDataOnExit || vm.ClearDataOnExitEnabled)
                {
                    foreach (var tab in vm.Tabs)
                    {
                        try
                        {
                            if (tab.WebView.CoreWebView2 != null)
                            {
                                _ = tab.WebView.CoreWebView2.Profile.ClearBrowsingDataAsync(
                                    Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.CacheStorage |
                                    Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.Cookies |
                                    Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.DiskCache);
                            }
                        }
                        catch { }
                    }
                    _ = ParadoxusBrowser.Data.DatabaseContext.Instance.ClearHistoryAsync();
                }
            }
        }

        #region Window Placement State Persistence

        public class WindowPlacementState
        {
            public double Width { get; set; } = 1200;
            public double Height { get; set; } = 800;
            public double Left { get; set; } = 100;
            public double Top { get; set; } = 100;
            public WindowState State { get; set; } = WindowState.Normal;
        }

        private static string WindowStateFilePath =>
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ParadoxusBrowser", "window_state.json");

        private void SaveWindowState()
        {
            try
            {
                var state = new WindowPlacementState
                {
                    State = WindowState
                };

                if (WindowState == WindowState.Maximized)
                {
                    state.Width = RestoreBounds.Width;
                    state.Height = RestoreBounds.Height;
                    state.Left = RestoreBounds.Left;
                    state.Top = RestoreBounds.Top;
                }
                else
                {
                    state.Width = ActualWidth;
                    state.Height = ActualHeight;
                    state.Left = Left;
                    state.Top = Top;
                }

                string folder = System.IO.Path.GetDirectoryName(WindowStateFilePath)!;
                System.IO.Directory.CreateDirectory(folder);
                string json = System.Text.Json.JsonSerializer.Serialize(state, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(WindowStateFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving window state: {ex.Message}");
            }
        }

        private void RestoreWindowState()
        {
            try
            {
                if (!System.IO.File.Exists(WindowStateFilePath))
                    return;

                string json = System.IO.File.ReadAllText(WindowStateFilePath);
                var state = System.Text.Json.JsonSerializer.Deserialize<WindowPlacementState>(json);
                if (state == null)
                    return;

                double virtualLeft = SystemParameters.VirtualScreenLeft;
                double virtualTop = SystemParameters.VirtualScreenTop;
                double virtualWidth = SystemParameters.VirtualScreenWidth;
                double virtualHeight = SystemParameters.VirtualScreenHeight;

                if (state.Width > 200 && state.Height > 200)
                {
                    Width = Math.Min(state.Width, virtualWidth);
                    Height = Math.Min(state.Height, virtualHeight);
                }

                if (state.Left >= virtualLeft && state.Left < virtualLeft + virtualWidth - 50)
                {
                    Left = state.Left;
                }

                if (state.Top >= virtualTop && state.Top < virtualTop + virtualHeight - 50)
                {
                    Top = state.Top;
                }

                if (state.State == WindowState.Maximized)
                {
                    WindowState = WindowState.Maximized;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error restoring window state: {ex.Message}");
            }
        }

        #endregion

        #region Fullscreen Immersion Mode (F11)

        public void ToggleFullscreen()
        {
            _isFullscreen = !_isFullscreen;
            if (_isFullscreen)
            {
                _preFullscreenState = WindowState;
                TitleBarBorder.Visibility = Visibility.Collapsed;
                NavToolbarBorder.Visibility = Visibility.Collapsed;
                BookmarksBarBorder.Visibility = Visibility.Collapsed;
                WindowState = WindowState.Maximized;
            }
            else
            {
                TitleBarBorder.Visibility = Visibility.Visible;
                NavToolbarBorder.Visibility = Visibility.Visible;
                BookmarksBarBorder.Visibility = Visibility.Visible;
                WindowState = _preFullscreenState;
            }
        }

        #endregion

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not MainViewModel vm)
                return;

            // F11: Fullscreen Immersion Mode
            if (e.Key == Key.F11)
            {
                ToggleFullscreen();
                e.Handled = true;
                return;
            }

            // F5 / Ctrl+F5: Reload / Reload ignoring cache
            if (e.Key == Key.F5)
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    vm.SelectedTab?.WebView.CoreWebView2?.Reload();
                }
                else
                {
                    vm.ReloadCommand.Execute(null);
                }
                e.Handled = true;
                return;
            }

            // F6 / Alt+D: Focus Omnibox
            if (e.Key == Key.F6 || (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.D))
            {
                OmniboxTextBox.Focus();
                OmniboxTextBox.SelectAll();
                e.Handled = true;
                return;
            }

            // Control modifier shortcuts
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                // Ctrl + Shift + T: Reopen last closed tab
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && e.Key == Key.T)
                {
                    vm.ReopenClosedTabCommand.Execute(null);
                    e.Handled = true;
                    return;
                }

                // Ctrl + Shift + Tab: Previous Tab
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && e.Key == Key.Tab)
                {
                    vm.SelectPreviousTab();
                    e.Handled = true;
                    return;
                }

                // Ctrl + Tab: Next Tab or MRU Tab
                if (e.Key == Key.Tab)
                {
                    if (vm.CycleTabsMruEnabled)
                    {
                        vm.SelectPreviousMruTab();
                    }
                    else
                    {
                        vm.SelectNextTab();
                    }
                    e.Handled = true;
                    return;
                }

                // Ctrl + 1..8: Select Tab by Index
                if (e.Key >= Key.D1 && e.Key <= Key.D8)
                {
                    int index = e.Key - Key.D1;
                    vm.SelectTabByIndex(index);
                    e.Handled = true;
                    return;
                }

                // Ctrl + 9: Select Last Tab
                if (e.Key == Key.D9)
                {
                    vm.SelectLastTab();
                    e.Handled = true;
                    return;
                }

                // Ctrl + T: New Tab
                if (e.Key == Key.T)
                {
                    vm.NewTabCommand.Execute(null);
                    e.Handled = true;
                    return;
                }

                // Ctrl + W / Ctrl + F4: Close Tab
                if (e.Key == Key.W || e.Key == Key.F4)
                {
                    vm.CloseTabCommand.Execute(vm.SelectedTab);
                    e.Handled = true;
                    return;
                }

                // Ctrl + N: New InPrivate Tab
                if (e.Key == Key.N)
                {
                    vm.NewPrivateTabCommand.Execute(null);
                    e.Handled = true;
                    return;
                }

                // Ctrl + H: History Window
                if (e.Key == Key.H)
                {
                    vm.ToggleHistoryCommand.Execute(null);
                    e.Handled = true;
                    return;
                }

                // Ctrl + J: Downloads Panel
                if (e.Key == Key.J)
                {
                    vm.ToggleDownloadsCommand.Execute(null);
                    e.Handled = true;
                    return;
                }

                // Ctrl + K: Command Palette
                if (e.Key == Key.K)
                {
                    vm.ToggleCommandPaletteCommand.Execute(null);
                    if (vm.IsCommandPaletteOpen)
                    {
                        Dispatcher.InvokeAsync(() =>
                        {
                            CommandPaletteSearchBox.Focus();
                            CommandPaletteSearchBox.SelectAll();
                        }, System.Windows.Threading.DispatcherPriority.Input);
                    }
                    e.Handled = true;
                    return;
                }

                // Ctrl + L: Focus Omnibox
                if (e.Key == Key.L)
                {
                    OmniboxTextBox.Focus();
                    OmniboxTextBox.SelectAll();
                    e.Handled = true;
                    return;
                }

                // Ctrl + R: Reload
                if (e.Key == Key.R)
                {
                    vm.ReloadCommand.Execute(null);
                    e.Handled = true;
                    return;
                }

                // Ctrl + ,: Settings
                if (e.Key == Key.OemComma)
                {
                    vm.ToggleSettingsCommand.Execute(null);
                    e.Handled = true;
                    return;
                }

                // Ctrl + Plus / Ctrl + Add: Zoom In
                if (e.Key == Key.OemPlus || e.Key == Key.Add)
                {
                    vm.SelectedTab?.ZoomInCommand.Execute(null);
                    e.Handled = true;
                    return;
                }

                // Ctrl + Minus / Ctrl + Subtract: Zoom Out
                if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
                {
                    vm.SelectedTab?.ZoomOutCommand.Execute(null);
                    e.Handled = true;
                    return;
                }

                // Ctrl + 0 / Ctrl + NumPad0: Reset Zoom (100%)
                if (e.Key == Key.D0 || e.Key == Key.NumPad0)
                {
                    vm.SelectedTab?.ResetZoomCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }
        }

        private void MainWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                if (DataContext is MainViewModel vm && vm.SelectedTab != null)
                {
                    if (e.Delta > 0)
                    {
                        vm.SelectedTab.ZoomIn();
                    }
                    else if (e.Delta < 0)
                    {
                        vm.SelectedTab.ZoomOut();
                    }
                    e.Handled = true;
                }
            }
        }

                private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                if (FindVisualParent<Button>(dep) != null)
                    return;

                var tabBorder = FindVisualParent<Border>(dep);
                while (tabBorder != null)
                {
                    if (tabBorder.Name == "TabBorder")
                        return;
                    tabBorder = FindVisualParent<Border>(VisualTreeHelper.GetParent(tabBorder));
                }
            }

            if (e.ClickCount == 2)
            {
                WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
                return;
            }

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                IntPtr hwnd = helper.Handle;

                if (WindowState == WindowState.Maximized)
                {
                    Point screenPoint = PointToScreen(e.GetPosition(this));
                    double percentX = e.GetPosition(this).X / ActualWidth;

                    SendMessage(hwnd, WM_SYSCOMMAND, (IntPtr)SC_RESTORE, IntPtr.Zero);

                    Left = screenPoint.X - (ActualWidth * percentX);
                    Top = screenPoint.Y - e.GetPosition(this).Y;

                    ReleaseCapture();
                    SendMessage(hwnd, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                }
                else
                {
                    ReleaseCapture();
                    SendMessage(hwnd, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                }
            }
        }

        private void OnMinimizeClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnMaximizeClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnTabSelected(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is TabViewModel tab)
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.SelectedTab = tab;
                }
            }
        }

        #region Tab Drag and Drop & Physics

        private void OnTabPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is TabViewModel tab)
            {
                _dragStartPoint = e.GetPosition(this);
                _draggedTab = tab;
                _draggedElement = element;
                _isDraggingTab = false;
                if (DataContext is MainViewModel vm)
                {
                    _sourceIndex = vm.Tabs.IndexOf(tab);
                    _targetIndex = _sourceIndex;
                }
            }
        }

        private void OnTabPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _draggedTab == null || _draggedElement == null)
                return;

            Point currentPos = e.GetPosition(this);
            Vector diff = currentPos - _dragStartPoint;

            if (!_isDraggingTab)
            {
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDraggingTab = true;
                    _draggedElement.CaptureMouse();
                    _adornerOffset = e.GetPosition(_draggedElement);

                    var layer = AdornerLayer.GetAdornerLayer(_draggedElement);
                    if (layer != null)
                    {
                        _dragAdorner = new TabDragAdorner(_draggedElement);
                        layer.Add(_dragAdorner);
                    }
                    _draggedElement.Opacity = 0.35;
                }
            }

            if (_isDraggingTab && _dragAdorner != null)
            {
                Point posInElement = e.GetPosition(_draggedElement);
                _dragAdorner.UpdatePosition(posInElement.X - _adornerOffset.X, posInElement.Y - _adornerOffset.Y);

                // Update visual insertion indicator and calculate targetIndex
                UpdateDropInsertionSlot(e.GetPosition(TabStripGrid));
            }
        }

        private void UpdateDropInsertionSlot(Point mousePosInContainer)
        {
            if (DataContext is not MainViewModel vm || vm.Tabs.Count == 0)
            {
                DropInsertionMarker.Visibility = Visibility.Collapsed;
                return;
            }

            int count = vm.Tabs.Count;
            int slot = -1;
            double markerX = 0;

            // 1. Check if past the right edge of the last tab (move to end)
            if (TabsItemsControl.ItemContainerGenerator.ContainerFromIndex(count - 1) is FrameworkElement lastContainer)
            {
                Point lastPt = lastContainer.TranslatePoint(new Point(0, 0), TabStripGrid);
                double lastRight = lastPt.X + lastContainer.ActualWidth;
                if (mousePosInContainer.X >= lastRight)
                {
                    slot = count;
                    markerX = lastRight;
                }
            }

            // 2. Check if before the left edge of the first tab
            if (slot == -1 && TabsItemsControl.ItemContainerGenerator.ContainerFromIndex(0) is FrameworkElement firstContainer)
            {
                Point firstPt = firstContainer.TranslatePoint(new Point(0, 0), TabStripGrid);
                if (mousePosInContainer.X <= firstPt.X)
                {
                    slot = 0;
                    markerX = firstPt.X;
                }
            }

            // 3. Check intermediate tabs
            if (slot == -1)
            {
                for (int i = 0; i < count; i++)
                {
                    if (TabsItemsControl.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement container)
                        continue;

                    Point pt = container.TranslatePoint(new Point(0, 0), TabStripGrid);
                    double left = pt.X;
                    double width = container.ActualWidth;
                    double right = left + width;
                    double center = left + (width / 2.0);

                    if (mousePosInContainer.X < right)
                    {
                        if (mousePosInContainer.X < center)
                        {
                            slot = i;
                            markerX = left;
                        }
                        else
                        {
                            slot = i + 1;
                            markerX = right;
                        }
                        break;
                    }
                }
            }

            // Fallback if not matched
            if (slot == -1)
            {
                slot = count;
                if (TabsItemsControl.ItemContainerGenerator.ContainerFromIndex(count - 1) is FrameworkElement lastC)
                {
                    Point p = lastC.TranslatePoint(new Point(0, 0), TabStripGrid);
                    markerX = p.X + lastC.ActualWidth;
                }
            }

            // Map slot (0..count) to targetIndex in collection
            if (slot <= _sourceIndex)
            {
                _targetIndex = slot;
            }
            else
            {
                _targetIndex = slot - 1;
            }

            if (_targetIndex < 0) _targetIndex = 0;
            if (_targetIndex >= count) _targetIndex = count - 1;

            // Position and show drop insertion marker
            Canvas.SetLeft(DropInsertionMarker, Math.Max(0, markerX - 1.5));
            DropInsertionMarker.Visibility = Visibility.Visible;
        }

        private void OnTabPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingTab)
            {
                DropInsertionMarker.Visibility = Visibility.Collapsed;

                if (_draggedElement != null)
                {
                    _draggedElement.ReleaseMouseCapture();
                    _draggedElement.Opacity = 1.0;
                    if (_dragAdorner != null)
                    {
                        var layer = AdornerLayer.GetAdornerLayer(_draggedElement);
                        layer?.Remove(_dragAdorner);
                        _dragAdorner = null;
                    }
                }

                if (DataContext is MainViewModel vm && _draggedTab != null)
                {
                    if (_targetIndex != _sourceIndex && _targetIndex >= 0 && _targetIndex < vm.Tabs.Count && _sourceIndex >= 0 && _sourceIndex < vm.Tabs.Count)
                    {
                        vm.Tabs.Move(_sourceIndex, _targetIndex);
                    }
                    vm.SelectedTab = _draggedTab;
                }

                _isDraggingTab = false;
                _draggedTab = null;
                _draggedElement = null;
                _sourceIndex = -1;
                _targetIndex = -1;
                e.Handled = true;
            }
            else
            {
                _draggedTab = null;
                _draggedElement = null;
                _sourceIndex = -1;
                _targetIndex = -1;
            }
        }

        private void OnTabStripMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            Point pt = e.GetPosition(TabStripScrollViewer);
            var hitResult = VisualTreeHelper.HitTest(TabStripScrollViewer, pt);
            if (hitResult?.VisualHit != null)
            {
                var tabBorder = FindVisualParent<Border>(hitResult.VisualHit);
                while (tabBorder != null && tabBorder.Name != "TabBorder")
                {
                    tabBorder = FindVisualParent<Border>(VisualTreeHelper.GetParent(tabBorder));
                }

                if (tabBorder == null)
                {
                    if (TabStripScrollViewer.ContextMenu != null)
                    {
                        TabStripScrollViewer.ContextMenu.PlacementTarget = TabStripScrollViewer;
                        TabStripScrollViewer.ContextMenu.IsOpen = true;
                        e.Handled = true;
                    }
                }
            }
        }

        private void OnCloseTabClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button closeButton)
                return;

            var tab = closeButton.DataContext as TabViewModel;
            if (tab == null || DataContext is not MainViewModel vm)
                return;

            Border? tabBorder = FindVisualParent<Border>(closeButton);
            while (tabBorder != null && tabBorder.Name != "TabBorder")
            {
                tabBorder = FindVisualParent<Border>(VisualTreeHelper.GetParent(tabBorder));
            }

            if (tabBorder != null)
            {
                var sb = new Storyboard();

                var widthAnim = new DoubleAnimation
                {
                    From = tabBorder.ActualWidth,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(130),
                    DecelerationRatio = 0.8
                };
                Storyboard.SetTarget(widthAnim, tabBorder);
                Storyboard.SetTargetProperty(widthAnim, new PropertyPath(FrameworkElement.WidthProperty));
                sb.Children.Add(widthAnim);

                var opacityAnim = new DoubleAnimation
                {
                    From = tabBorder.Opacity,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(100)
                };
                Storyboard.SetTarget(opacityAnim, tabBorder);
                Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(UIElement.OpacityProperty));
                sb.Children.Add(opacityAnim);

                sb.Completed += (s, ev) =>
                {
                    vm.CloseTab(tab);
                };

                sb.Begin();
            }
            else
            {
                vm.CloseTab(tab);
            }
        }

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                    return parent;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        #endregion

        #region Command Palette Events

        private void OnCommandPaletteBackdropMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.IsCommandPaletteOpen = false;
            }
        }

        private void OnCommandPaletteContainerMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void OnCommandPaletteKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not MainViewModel vm)
                return;

            if (e.Key == Key.Escape)
            {
                vm.IsCommandPaletteOpen = false;
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                vm.ExecutePaletteItemCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                if (CommandPaletteListBox.Items.Count > 0)
                {
                    int idx = CommandPaletteListBox.SelectedIndex;
                    if (idx < CommandPaletteListBox.Items.Count - 1)
                        CommandPaletteListBox.SelectedIndex = idx + 1;
                    CommandPaletteListBox.ScrollIntoView(CommandPaletteListBox.SelectedItem);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (CommandPaletteListBox.Items.Count > 0)
                {
                    int idx = CommandPaletteListBox.SelectedIndex;
                    if (idx > 0)
                        CommandPaletteListBox.SelectedIndex = idx - 1;
                    CommandPaletteListBox.ScrollIntoView(CommandPaletteListBox.SelectedItem);
                }
                e.Handled = true;
            }
        }

        private void OnCommandPaletteListBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not MainViewModel vm)
                return;

            if (e.Key == Key.Escape)
            {
                vm.IsCommandPaletteOpen = false;
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                vm.ExecutePaletteItemCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void OnCommandPaletteItemDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.ExecutePaletteItemCommand.Execute(null);
            }
        }

        #endregion

                private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;

            if (vm.IsOmniboxSuggestionsOpen && e.OriginalSource is DependencyObject dep)
            {
                var parentBorder = FindVisualParent<Border>(dep);
                bool inOmnibox = false;
                while (parentBorder != null)
                {
                    if (parentBorder.Name == "OmniboxBorder")
                    {
                        inOmnibox = true;
                        break;
                    }
                    parentBorder = FindVisualParent<Border>(VisualTreeHelper.GetParent(parentBorder));
                }
                if (!inOmnibox && FindVisualParent<ListBoxItem>(dep) == null && FindVisualParent<ListBox>(dep) == null)
                {
                    vm.IsOmniboxSuggestionsOpen = false;
                }
            }
        }

                private void OnOmniboxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                _ = vm.LoadOmniboxSuggestionsAsync(OmniboxTextBox.Text);
            }
        }

        private void OnOmniboxLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;

            if (e.NewFocus is DependencyObject newFocus)
            {
                if (FindVisualParent<ListBoxItem>(newFocus) != null || FindVisualParent<ListBox>(newFocus) != null)
                    return;
            }

            vm.IsOmniboxSuggestionsOpen = false;
        }

        private void OnOmniboxGotFocus(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                _ = vm.LoadOmniboxSuggestionsAsync(OmniboxTextBox.Text);
            }
        }

        private void OnOmniboxTextChanged(object sender, TextChangedEventArgs e)
        {
            if (OmniboxTextBox.IsFocused && DataContext is MainViewModel vm)
            {
                _ = vm.LoadOmniboxSuggestionsAsync(OmniboxTextBox.Text);
            }
        }

        private void OnOmniboxSuggestionClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                if (FindVisualParent<Button>(dep) != null)
                    return;

                var item = FindVisualParent<ListBoxItem>(dep);
                if (item?.DataContext is SearchHistoryItem historyItem && DataContext is MainViewModel vm)
                {
                    vm.SelectOmniboxSuggestionCommand.Execute(historyItem);
                }
            }
        }

        private void OnOmniboxKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;

            if (e.Key == Key.Down)
            {
                if (vm.IsOmniboxSuggestionsOpen && vm.OmniboxSuggestions.Count > 0)
                {
                    int index = vm.SelectedOmniboxSuggestion != null ? vm.OmniboxSuggestions.IndexOf(vm.SelectedOmniboxSuggestion) : -1;
                    if (index < vm.OmniboxSuggestions.Count - 1)
                    {
                        vm.SelectedOmniboxSuggestion = vm.OmniboxSuggestions[index + 1];
                    }
                    else
                    {
                        vm.SelectedOmniboxSuggestion = vm.OmniboxSuggestions[0];
                    }
                    e.Handled = true;
                    return;
                }
            }
            else if (e.Key == Key.Up)
            {
                if (vm.IsOmniboxSuggestionsOpen && vm.OmniboxSuggestions.Count > 0)
                {
                    int index = vm.SelectedOmniboxSuggestion != null ? vm.OmniboxSuggestions.IndexOf(vm.SelectedOmniboxSuggestion) : vm.OmniboxSuggestions.Count;
                    if (index > 0)
                    {
                        vm.SelectedOmniboxSuggestion = vm.OmniboxSuggestions[index - 1];
                    }
                    else
                    {
                        vm.SelectedOmniboxSuggestion = vm.OmniboxSuggestions[^1];
                    }
                    e.Handled = true;
                    return;
                }
            }
            else if (e.Key == Key.Escape)
            {
                if (vm.IsOmniboxSuggestionsOpen)
                {
                    vm.IsOmniboxSuggestionsOpen = false;
                    e.Handled = true;
                    return;
                }
            }
            else if (e.Key == Key.Enter)
            {
                if (vm.IsOmniboxSuggestionsOpen && vm.SelectedOmniboxSuggestion != null)
                {
                    vm.SelectOmniboxSuggestionCommand.Execute(vm.SelectedOmniboxSuggestion);
                    e.Handled = true;
                    return;
                }

                vm.IsOmniboxSuggestionsOpen = false;
                vm.NavigateCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void OnHistoryDimmerClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.IsHistoryOpen = false;
            }
        }

        private void OnHistoryItemClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is HistoryItem item)
            {
                if (DataContext is MainViewModel vm && vm.SelectedTab != null)
                {
                    vm.SelectedTab.Navigate(item.Url);
                    vm.IsHistoryOpen = false;
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (DataContext is MainViewModel vm)
            {
                foreach (var tab in vm.Tabs)
                {
                    tab.Dispose();
                }
            }
        }
    }

    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility vis && vis == Visibility.Collapsed;
        }
    }

    public class EqualityMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] != null && values[1] != null)
            {
                return ReferenceEquals(values[0], values[1]);
            }
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TabDragAdorner : Adorner
    {
        private readonly VisualBrush _visualBrush;
        private double _leftOffset;
        private double _topOffset;
        private readonly double _width;
        private readonly double _height;

        public TabDragAdorner(UIElement adornedElement) : base(adornedElement)
        {
            _width = adornedElement.RenderSize.Width;
            _height = adornedElement.RenderSize.Height;
            _visualBrush = new VisualBrush(adornedElement)
            {
                Opacity = 0.88,
                Stretch = Stretch.None
            };
            IsHitTestVisible = false;
        }

        public void UpdatePosition(double left, double top)
        {
            _leftOffset = left;
            _topOffset = top;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            var shadowRect = new Rect(_leftOffset + 2, _topOffset + 3, _width, _height);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)), null, shadowRect, 6, 6);
            var rect = new Rect(_leftOffset, _topOffset, _width, _height);
            dc.DrawRoundedRectangle(_visualBrush, new Pen(new SolidColorBrush(Color.FromArgb(140, 255, 118, 0)), 1.5), rect, 6, 6);
        }
    }
}