using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DesktopToolbar.Services;
using WinForms = System.Windows.Forms;
using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;

namespace DesktopToolbar
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_NOACTIVATE = 3;
        private const double RevealTabHitWidth = 84;
        private const double RevealTabHitHeight = 14;
        private const double TopSnapThreshold = 24;

        private bool _isAutoHideEnabled = true;
        private bool _isExpanded = true;
        private bool _isSnappedToTop = true;
        private bool _isDraggingWindow;
        private double _arrangePreviousLeft;
        private double _arrangePreviousTop;
        private double _arrangePreviousExpandedTop;
        private double _arrangePreviousDockedTop;
        private bool _arrangePreviousIsExpanded;
        private bool _arrangePreviousIsSnappedToTop;
        private DispatcherTimer? _hoverCheckTimer;
        private DispatcherTimer? _hideDelayTimer;
        private double _dockedTop;
        private double _expandedTop;

        private ToolbarConfig _config = new();
        private readonly string _configPath;
        private WinForms.NotifyIcon? _trayIcon;
        private WinForms.ContextMenuStrip? _trayMenu;
        private WinForms.ToolStripMenuItem? _trayTopmostItem;
        private WinForms.ToolStripMenuItem? _trayArrangeItem;
        private bool _isArrangeMode;
        private LauncherItem? _draggedLauncher;
        private WpfPoint _dragStartPoint;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Visibility TextVisibility =>
            _config.ToolbarSettings.DisplayMode == "IconsOnly" ? Visibility.Collapsed : Visibility.Visible;

        public string ToolbarTitle => _config.ToolbarSettings.TitleText;

        public Visibility ToolbarTitleVisibility =>
            _config.ToolbarSettings.ShowTitle && !string.IsNullOrWhiteSpace(_config.ToolbarSettings.TitleText)
                ? Visibility.Visible
                : Visibility.Collapsed;

        public MainWindow()
        {
            InitializeComponent();

            Icon = IconService.GetAppIconImage(64);

            string appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SideBar");
            Directory.CreateDirectory(appDataDir);
            _configPath = Path.Combine(appDataDir, "toolbar_config.json");

            this.DataContext = this;

            SetupAutoHide();
            LoadConfiguration();
            SetupSystemTray();
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void NotifyLayoutPropertiesChanged()
        {
            OnPropertyChanged(nameof(TextVisibility));
            OnPropertyChanged(nameof(ToolbarTitle));
            OnPropertyChanged(nameof(ToolbarTitleVisibility));
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            ApplyTrayMenuTheme();
        }

        private void ApplyTrayMenuTheme()
        {
            if (_trayMenu != null)
            {
                _trayMenu.Renderer = new FluentTrayMenuRenderer(ThemeManager.IsDarkTheme);
            }
        }

        private void SetupAutoHide()
        {
            _hoverCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _hoverCheckTimer.Tick += (_, _) => CheckMouseProximity();

            _hideDelayTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(550)
            };
            _hideDelayTimer.Tick += (_, _) =>
            {
                _hideDelayTimer.Stop();
                if (IsLoaded && IsVisible &&
                    _isAutoHideEnabled && !_isArrangeMode && _isSnappedToTop && !_isDraggingWindow &&
                    !IsCursorInRevealZone() && !this.IsMouseOver)
                {
                    SlideWindow(false);
                }
            };
        }

        private void CheckMouseProximity()
        {
            if (!IsLoaded || !IsVisible || !_isAutoHideEnabled || !_isSnappedToTop || _isArrangeMode || _isDraggingWindow)
            {
                return;
            }

            bool isInRevealZone = !_isExpanded && IsCursorInRevealZone();
            bool isOverWindow = this.IsMouseOver;

            if ((isInRevealZone || isOverWindow) && !_isExpanded)
            {
                _hideDelayTimer?.Stop();
                SlideWindow(true);
            }
            else if (!isOverWindow && _isExpanded)
            {
                if (_hideDelayTimer != null && !_hideDelayTimer.IsEnabled)
                {
                    _hideDelayTimer.Start();
                }
            }
            else if (isInRevealZone || isOverWindow)
            {
                _hideDelayTimer?.Stop();
            }
        }

        private bool IsCursorInRevealZone()
        {
            if (!_isSnappedToTop || _isExpanded || PresentationSource.FromVisual(this) == null)
            {
                return false;
            }

            try
            {
                var mousePosition = WinForms.Cursor.Position;
                WpfPoint localPosition = PointFromScreen(new WpfPoint(mousePosition.X, mousePosition.Y));
                double tabLeft = (ActualWidth - RevealTabHitWidth) / 2;

                return localPosition.X >= tabLeft &&
                       localPosition.X <= tabLeft + RevealTabHitWidth &&
                       localPosition.Y >= ActualHeight - RevealTabHitHeight &&
                       localPosition.Y <= ActualHeight + 4;
            }
            catch
            {
                return false;
            }
        }

        private void SlideWindow(bool expand)
        {
            if (!IsLoaded || !IsVisible || _isExpanded == expand || (!expand && !_isSnappedToTop)) return;
            _isExpanded = expand;
            RevealTab.Visibility = expand ? Visibility.Collapsed : Visibility.Visible;
            ToolbarSurface.IsHitTestVisible = expand;

            UpdateDockedMetrics();
            double targetTop = expand ? _expandedTop : _dockedTop;
            double fromTop = double.IsNaN(this.Top) || double.IsInfinity(this.Top)
                ? (expand ? _dockedTop : _expandedTop)
                : this.Top;

            TimeSpan duration = TimeSpan.FromMilliseconds(220);

            var slideAnimation = new DoubleAnimation
            {
                From = fromTop,
                To = targetTop,
                Duration = duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var surfaceAnimation = new DoubleAnimation
            {
                From = double.IsNaN(ToolbarSurface.Opacity) ? (expand ? 0 : 1) : ToolbarSurface.Opacity,
                To = expand ? 1 : 0,
                Duration = duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            this.BeginAnimation(Window.TopProperty, slideAnimation);
            ToolbarSurface.BeginAnimation(UIElement.OpacityProperty, surfaceAnimation);
        }

        private void UpdateDockedMetrics()
        {
            double effectiveHeight = ActualHeight > 0 ? ActualHeight : 76;
            _expandedTop = 0;
            _dockedTop = -effectiveHeight + 10;
        }

        private void SetupSystemTray()
        {
            _trayIcon = new WinForms.NotifyIcon
            {
                Icon = IconService.CreateTrayIcon(),
                Text = "PhantomCore SideBar",
                Visible = true
            };

            _trayMenu = new WinForms.ContextMenuStrip
            {
                ShowImageMargin = true,
                Font = new System.Drawing.Font("Segoe UI", 9.5f)
            };
            ApplyTrayMenuTheme();

            _trayTopmostItem = new WinForms.ToolStripMenuItem("Always on Top", null, (_, _) => ToggleAlwaysOnTop())
            {
                Checked = _config.ToolbarSettings.AlwaysOnTop,
                CheckOnClick = true
            };
            _trayMenu.Items.Add(_trayTopmostItem);

            _trayArrangeItem = new WinForms.ToolStripMenuItem("Arrange Icons", null, (_, _) => ToggleArrangeMode())
            {
                CheckOnClick = true
            };
            _trayMenu.Items.Add(_trayArrangeItem);

            _trayMenu.Items.Add(new WinForms.ToolStripSeparator());
            _trayMenu.Items.Add("Edit Shortcuts...", null, (_, _) => ShowShortcutManagerWindow());
            _trayMenu.Items.Add("Toolbar Settings...", null, (_, _) => ShowSettingsWindow());
            _trayMenu.Items.Add("Reload Config", null, (_, _) => LoadConfiguration());
            _trayMenu.Items.Add("Open Config File", null, (_, _) => OpenConfigFile());
            _trayMenu.Items.Add("About PhantomCore SideBar...", null, (_, _) => ShowAboutWindow());
            _trayMenu.Items.Add(new WinForms.ToolStripSeparator());
            _trayMenu.Items.Add("Exit PhantomCore SideBar", null, (_, _) => System.Windows.Application.Current.Shutdown());

            _trayIcon.ContextMenuStrip = _trayMenu;
            _trayIcon.DoubleClick += (_, _) =>
            {
                if (_isSnappedToTop && !_isExpanded)
                {
                    SlideWindow(true);
                }
                else
                {
                    PositionAtTop();
                }
            };
        }

        private void ToggleArrangeMode()
        {
            bool arrangeMode = _trayArrangeItem?.Checked == true;
            _draggedLauncher = null;

            if (arrangeMode == _isArrangeMode)
            {
                return;
            }

            if (arrangeMode)
            {
                _arrangePreviousLeft = Left;
                _arrangePreviousTop = _isSnappedToTop
                    ? (_isExpanded ? _expandedTop : _dockedTop)
                    : Top;
                _arrangePreviousExpandedTop = _expandedTop;
                _arrangePreviousDockedTop = _dockedTop;
                _arrangePreviousIsExpanded = _isExpanded;
                _arrangePreviousIsSnappedToTop = _isSnappedToTop;

                _isArrangeMode = true;
                _hideDelayTimer?.Stop();
                SetAutoHideEnabled(_config.ToolbarSettings.AutoHideEnabled);

                Rect workArea = SystemParameters.WorkArea;
                _isSnappedToTop = false;
                _isExpanded = true;
                double effectiveHeight = ActualHeight > 0 ? ActualHeight : 76;
                double effectiveWidth = ActualWidth > 0 ? ActualWidth : 400;
                _expandedTop = workArea.Bottom - effectiveHeight;
                _dockedTop = _expandedTop;
                this.BeginAnimation(Window.TopProperty, null);
                Left = workArea.Left + (workArea.Width - effectiveWidth) / 2;
                Top = _expandedTop;
                RevealTab.Visibility = Visibility.Collapsed;
                ToolbarSurface.BeginAnimation(UIElement.OpacityProperty, null);
                ToolbarSurface.Opacity = 1;
                ToolbarSurface.IsHitTestVisible = true;
            }
            else
            {
                _isArrangeMode = false;
                SetAutoHideEnabled(_config.ToolbarSettings.AutoHideEnabled);

                ToolbarSurface.BeginAnimation(UIElement.OpacityProperty, null);
                this.BeginAnimation(Window.TopProperty, null);
                Left = _arrangePreviousLeft;
                _expandedTop = _arrangePreviousExpandedTop;
                _dockedTop = _arrangePreviousDockedTop;
                _isSnappedToTop = _arrangePreviousIsSnappedToTop;
                _isExpanded = _arrangePreviousIsExpanded;
                Top = _arrangePreviousTop;
                RevealTab.Visibility = _isExpanded ? Visibility.Collapsed : Visibility.Visible;
                ToolbarSurface.Opacity = _isExpanded ? 1 : 0;
                ToolbarSurface.IsHitTestVisible = _isExpanded;
            }

            Cursor = _isArrangeMode ? System.Windows.Input.Cursors.SizeAll : System.Windows.Input.Cursors.Arrow;
        }

        private void ToggleAlwaysOnTop()
        {
            if (_trayTopmostItem != null)
            {
                _config.ToolbarSettings.AlwaysOnTop = _trayTopmostItem.Checked;
                this.Topmost = _config.ToolbarSettings.AlwaysOnTop;
                SaveConfiguration();
            }
        }

        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    _config = JsonSerializer.Deserialize<ToolbarConfig>(json) ?? new ToolbarConfig();
                }
                else
                {
                    _config = new ToolbarConfig();
                    _config.LauncherItems.Add(new LauncherItem { Label = "Terminal", Executable = "wt.exe" });
                    _config.LauncherItems.Add(new LauncherItem { Label = "Explorer", Executable = "explorer.exe" });
                    SaveConfiguration();
                }
            }
            catch
            {
                _config = new ToolbarConfig();
            }

            foreach (var item in _config.LauncherItems)
            {
                item.ClearIconCache();
            }

            ApplySettingsToUI();
            RefreshLauncherItems();
        }

        private void ApplySettingsToUI()
        {
            ThemeManager.SetThemeMode(_config.ToolbarSettings.ThemeMode);
            SetAutoHideEnabled(_config.ToolbarSettings.AutoHideEnabled);

            this.Topmost = _config.ToolbarSettings.AlwaysOnTop;

            if (_trayTopmostItem != null)
            {
                _trayTopmostItem.Checked = _config.ToolbarSettings.AlwaysOnTop;
            }

            if (MenuLockToggle != null)
            {
                MenuLockToggle.IsChecked = _config.ToolbarSettings.IsLocked;
            }

            if (MenuModeIconsAndText != null && MenuModeIconsOnly != null)
            {
                bool isIconsOnly = _config.ToolbarSettings.DisplayMode == "IconsOnly";
                MenuModeIconsOnly.IsChecked = isIconsOnly;
                MenuModeIconsAndText.IsChecked = !isIconsOnly;
            }

            if (DragHandleIcon != null)
            {
                DragHandleIcon.Cursor = _config.ToolbarSettings.IsLocked
                    ? System.Windows.Input.Cursors.Arrow
                    : System.Windows.Input.Cursors.SizeAll;
                DragHandleIcon.Opacity = _config.ToolbarSettings.IsLocked ? 0.25 : 0.65;
            }

            NotifyLayoutPropertiesChanged();
        }

        private void RefreshLauncherItems()
        {
            LauncherItemsControl.ItemsSource = null;
            LauncherItemsControl.ItemsSource = _config.LauncherItems;
        }

        private void SaveConfiguration()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_config, options);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, $"Failed to save config: {ex.Message}",
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            WindowHelper.ApplyNonActivatingToolWindowStyle(this);

            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            HwndSource? source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_MOUSEACTIVATE)
            {
                handled = true;
                return new IntPtr(MA_NOACTIVATE);
            }
            return IntPtr.Zero;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            PositionAtTop();
            _hoverCheckTimer?.Start();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isSnappedToTop && !_isDraggingWindow && !_isArrangeMode && e.WidthChanged)
            {
                Rect workArea = SystemParameters.WorkArea;
                Left = workArea.Left + (workArea.Width - ActualWidth) / 2;
                UpdateDockedMetrics();
            }
        }

        private void PositionAtTop()
        {
            Rect workArea = SystemParameters.WorkArea;
            UpdateDockedMetrics();

            _isSnappedToTop = true;
            _isExpanded = true;

            this.BeginAnimation(Window.TopProperty, null);
            ToolbarSurface.BeginAnimation(UIElement.OpacityProperty, null);
            ToolbarSurface.Opacity = 1;
            ToolbarSurface.IsHitTestVisible = true;
            RevealTab.Visibility = Visibility.Collapsed;

            double effectiveWidth = ActualWidth > 0 ? ActualWidth : 400;
            this.Left = workArea.Left + (workArea.Width - effectiveWidth) / 2;
            this.Top = _expandedTop;
        }

        protected override void OnClosed(EventArgs e)
        {
            _hoverCheckTimer?.Stop();
            _hideDelayTimer?.Stop();
            ThemeManager.ThemeChanged -= OnThemeChanged;

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            _trayMenu?.Dispose();
            _trayMenu = null;

            base.OnClosed(e);
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && !_config.ToolbarSettings.IsLocked)
            {
                _isDraggingWindow = true;
                _hideDelayTimer?.Stop();
                this.BeginAnimation(Window.TopProperty, null);
                try
                {
                    this.DragMove();
                }
                catch
                {
                    // DragMove can throw if mouse button was already released
                }
                finally
                {
                    _isDraggingWindow = false;
                    CompleteWindowDrag();
                }
            }
        }

        private void CompleteWindowDrag()
        {
            _isExpanded = true;
            RevealTab.Visibility = Visibility.Collapsed;
            ToolbarSurface.BeginAnimation(UIElement.OpacityProperty, null);
            ToolbarSurface.Opacity = 1;
            ToolbarSurface.IsHitTestVisible = true;

            if (Math.Abs(Top) <= TopSnapThreshold)
            {
                _isSnappedToTop = true;
                UpdateDockedMetrics();
                Top = _expandedTop;
            }
            else
            {
                _isSnappedToTop = false;
            }
        }

        private void Toolbar_DragOver(object sender, WpfDragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
                DropHint.Visibility = Visibility.Visible;
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }

            e.Handled = true;
        }

        private void Toolbar_DragLeave(object sender, WpfDragEventArgs e)
        {
            DropHint.Visibility = Visibility.Collapsed;
        }

        private void Toolbar_Drop(object sender, WpfDragEventArgs e)
        {
            DropHint.Visibility = Visibility.Collapsed;

            if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) ||
                e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] paths)
            {
                return;
            }

            int addedCount = 0;
            foreach (string path in paths)
            {
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    continue;
                }

                string fullPath = Path.GetFullPath(path);
                bool alreadyPinned = _config.LauncherItems.Exists(item =>
                    string.Equals(item.Executable, fullPath, StringComparison.OrdinalIgnoreCase));
                if (alreadyPinned)
                {
                    continue;
                }

                string label = Directory.Exists(fullPath)
                    ? new DirectoryInfo(fullPath).Name
                    : Path.GetFileNameWithoutExtension(fullPath);
                if (string.IsNullOrWhiteSpace(label))
                {
                    label = Path.GetFileName(fullPath);
                }

                _config.LauncherItems.Add(new LauncherItem
                {
                    Label = label,
                    Executable = fullPath
                });
                addedCount++;
            }

            if (addedCount > 0)
            {
                SaveConfiguration();
                RefreshLauncherItems();
            }

            e.Handled = true;
        }

        private void LauncherButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isArrangeMode)
            {
                return;
            }

            if (sender is FrameworkElement fe && fe.DataContext is LauncherItem item &&
                !string.IsNullOrWhiteSpace(item.Executable))
            {
                try
                {
                    string fileName = Environment.ExpandEnvironmentVariables(item.Executable.Trim());
                    string workingDir = string.IsNullOrWhiteSpace(item.WorkingDirectory)
                        ? (File.Exists(fileName) ? Path.GetDirectoryName(fileName) ?? "" : "")
                        : Environment.ExpandEnvironmentVariables(item.WorkingDirectory.Trim());

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = item.Arguments,
                        UseShellExecute = true
                    };

                    if (!string.IsNullOrWhiteSpace(workingDir) && Directory.Exists(workingDir))
                    {
                        startInfo.WorkingDirectory = workingDir;
                    }

                    Process.Start(startInfo);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(this, $"Failed to launch {item.Label}: {ex.Message}",
                        "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LauncherButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isArrangeMode || sender is not FrameworkElement element || element.DataContext is not LauncherItem item)
            {
                return;
            }

            _draggedLauncher = item;
            _dragStartPoint = e.GetPosition(null);
        }

        private void LauncherButton_PreviewMouseMove(object sender, WpfMouseEventArgs e)
        {
            if (!_isArrangeMode || e.LeftButton != MouseButtonState.Pressed || _draggedLauncher == null)
            {
                return;
            }

            WpfPoint currentPoint = e.GetPosition(null);
            Vector movement = currentPoint - _dragStartPoint;
            if (Math.Abs(movement.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(movement.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            System.Windows.DataObject data = new(typeof(LauncherItem), _draggedLauncher);
            DragDrop.DoDragDrop((DependencyObject)sender, data, System.Windows.DragDropEffects.Move);
            _draggedLauncher = null;
        }

        private void LauncherButton_Drop(object sender, WpfDragEventArgs e)
        {
            if (!_isArrangeMode || _draggedLauncher == null ||
                sender is not FrameworkElement element || element.DataContext is not LauncherItem target ||
                ReferenceEquals(_draggedLauncher, target))
            {
                return;
            }

            int oldIndex = _config.LauncherItems.IndexOf(_draggedLauncher);
            int newIndex = _config.LauncherItems.IndexOf(target);
            if (oldIndex < 0 || newIndex < 0)
            {
                return;
            }

            _config.LauncherItems.RemoveAt(oldIndex);
            if (oldIndex < newIndex)
            {
                newIndex--;
            }
            _config.LauncherItems.Insert(newIndex, _draggedLauncher);
            _draggedLauncher = null;

            SaveConfiguration();
            RefreshLauncherItems();
            e.Handled = true;
        }

        private void LauncherButton_DragOver(object sender, WpfDragEventArgs e)
        {
            if (_isArrangeMode && _draggedLauncher != null && e.Data.GetDataPresent(typeof(LauncherItem)))
            {
                e.Effects = System.Windows.DragDropEffects.Move;
                e.Handled = true;
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
        }

        private void Menu_Reload_Click(object sender, RoutedEventArgs e) => LoadConfiguration();

        private void Menu_ToggleLock_Click(object sender, RoutedEventArgs e)
        {
            _config.ToolbarSettings.IsLocked = MenuLockToggle.IsChecked;
            SaveConfiguration();
            ApplySettingsToUI();
        }

        private void Menu_ToggleAutoHide_Click(object sender, RoutedEventArgs e)
        {
            SetAutoHideEnabled(MenuAutoHideToggle.IsChecked);
            SaveConfiguration();
        }

        private void SetAutoHideEnabled(bool isEnabled)
        {
            _isAutoHideEnabled = isEnabled && !_isArrangeMode;
            _config.ToolbarSettings.AutoHideEnabled = isEnabled;
            if (MenuAutoHideToggle != null)
            {
                MenuAutoHideToggle.IsChecked = isEnabled;
            }

            if (!_isAutoHideEnabled)
            {
                _hideDelayTimer?.Stop();
                if (!_isExpanded && _isSnappedToTop)
                {
                    SlideWindow(true);
                }
            }
        }

        private void Menu_ModeIconsAndText_Click(object sender, RoutedEventArgs e)
        {
            _config.ToolbarSettings.DisplayMode = "IconsAndText";
            SaveConfiguration();
            ApplySettingsToUI();
        }

        private void Menu_ModeIconsOnly_Click(object sender, RoutedEventArgs e)
        {
            _config.ToolbarSettings.DisplayMode = "IconsOnly";
            SaveConfiguration();
            ApplySettingsToUI();
        }

        private void Menu_Close_Click(object sender, RoutedEventArgs e) => System.Windows.Application.Current.Shutdown();
        private void Menu_EditConfig_Click(object sender, RoutedEventArgs e) => ShowSettingsWindow();
        private void Menu_About_Click(object sender, RoutedEventArgs e) => ShowAboutWindow();
        private void Menu_EditShortcuts_Click(object sender, RoutedEventArgs e) => ShowShortcutManagerWindow();

        private void ShowAboutWindow()
        {
            var aboutWindow = new AboutWindow
            {
                Owner = this,
                Topmost = this.Topmost,
                Icon = this.Icon
            };
            aboutWindow.ShowDialog();
        }

        private void ShowSettingsWindow()
        {
            var settingsWindow = new SettingsWindow(_config.ToolbarSettings)
            {
                Owner = this,
                Topmost = this.Topmost,
                Icon = this.Icon
            };

            if (settingsWindow.ShowDialog() == true && settingsWindow.UpdatedSettings is ToolbarSettings updatedSettings)
            {
                _config.ToolbarSettings = updatedSettings;
                SaveConfiguration();
                ApplySettingsToUI();

                if (_isArrangeMode)
                {
                    Rect workArea = SystemParameters.WorkArea;
                    double effectiveHeight = ActualHeight > 0 ? ActualHeight : 76;
                    double effectiveWidth = ActualWidth > 0 ? ActualWidth : 400;
                    _expandedTop = workArea.Bottom - effectiveHeight;
                    _dockedTop = _expandedTop;
                    Left = workArea.Left + (workArea.Width - effectiveWidth) / 2;
                    Top = _expandedTop;
                }
                else if (_isSnappedToTop)
                {
                    PositionAtTop();
                }
            }
        }

        private void ShowShortcutManagerWindow()
        {
            var manager = new ShortcutManagerWindow(_config.LauncherItems, PromptEditLauncher)
            {
                Owner = this,
                Topmost = this.Topmost,
                Icon = this.Icon
            };

            if (manager.ShowDialog() == true && manager.UpdatedItems is not null)
            {
                _config.LauncherItems.Clear();
                _config.LauncherItems.AddRange(manager.UpdatedItems);
                SaveConfiguration();
                RefreshLauncherItems();
            }
        }

        private void OpenConfigFile()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _configPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, $"Could not open config file: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Menu_AddLauncher_Click(object sender, RoutedEventArgs e)
        {
            var newItem = new LauncherItem { Label = "New App", Executable = "cmd.exe", Arguments = "" };
            if (PromptEditLauncher(this, "Add New Launcher", newItem))
            {
                _config.LauncherItems.Add(newItem);
                SaveConfiguration();
                RefreshLauncherItems();
            }
        }

        private void ItemMenu_Edit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem mi && mi.DataContext is LauncherItem item)
            {
                if (PromptEditLauncher(this, "Edit Launcher", item))
                {
                    SaveConfiguration();
                    RefreshLauncherItems();
                }
            }
        }

        private void ItemMenu_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem mi && mi.DataContext is LauncherItem item)
            {
                if (System.Windows.MessageBox.Show(this, $"Are you sure you want to remove '{item.Label}'?",
                        "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _config.LauncherItems.Remove(item);
                    SaveConfiguration();
                    RefreshLauncherItems();
                }
            }
        }

        private static bool PromptEditLauncher(Window owner, string title, LauncherItem item)
        {
            var dialog = new LauncherEditorWindow(title, item)
            {
                Owner = owner,
                Topmost = owner.Topmost,
                Icon = owner.Icon
            };
            return dialog.ShowDialog() == true;
        }
    }
}