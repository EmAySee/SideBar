using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;
using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;

namespace DesktopToolbar
{
    public class ToolbarConfig
    {
        public ToolbarSettings ToolbarSettings { get; set; } = new();
        public List<LauncherItem> LauncherItems { get; set; } = new();
    }

    public class ToolbarSettings
    {
        public double Height { get; set; } = 44;
        public double Width { get; set; } = 750;
        public double BottomOffset { get; set; } = 0;
        public double CornerRadius { get; set; } = 6;
        public string DisplayMode { get; set; } = "IconsAndText";
        public bool IsLocked { get; set; } = false;
        public bool AlwaysOnTop { get; set; } = true;
        public bool AutoHideEnabled { get; set; } = true;
        public string TitleText { get; set; } = "LP:";
        public bool ShowTitle { get; set; } = true;
    }

    public class LauncherItem
    {
        public const string Shell32IconPrefix = "Shell32:";

        public static readonly IReadOnlyDictionary<string, int> BuiltInIconIndexes = new Dictionary<string, int>
        {
            ["Application"] = 2,
            ["Folder"] = 3,
            ["Folder Open"] = 4,
            ["Computer"] = 15,
            ["Search"] = 22,
            ["Network"] = 17,
            ["Control Panel"] = 21,
            ["Document"] = 1,
            ["Warning"] = 78
        };

        public string Label { get; set; } = "";
        public string Executable { get; set; } = "";
        public string Arguments { get; set; } = "";
        public string IconPath { get; set; } = "";
        public string BuiltInIcon { get; set; } = "";

        private System.Windows.Media.ImageSource? _cachedIcon;
        private bool _iconResolved = false;

        [System.Text.Json.Serialization.JsonIgnore]
        public System.Windows.Media.ImageSource? AppIcon
        {
            get
            {
                if (!_iconResolved)
                {
                    _cachedIcon = ResolveAppIcon();
                    _iconResolved = true;
                }
                return _cachedIcon;
            }
        }

        public void ClearIconCache()
        {
            _cachedIcon = null;
            _iconResolved = false;
        }

        private System.Windows.Media.ImageSource? ResolveAppIcon()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(IconPath) && File.Exists(IconPath))
                {
                    if (IconPath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var icon = new System.Drawing.Icon(IconPath))
                        {
                            var bmpSource = Imaging.CreateBitmapSourceFromHIcon(
                                icon.Handle,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromWidthAndHeight(56, 56));
                            bmpSource.Freeze();
                            return bmpSource;
                        }
                    }
                    else
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(IconPath, UriKind.Absolute);
                        bitmap.DecodePixelWidth = 56;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        return bitmap;
                    }
                }

                if (TryGetShell32Index(BuiltInIcon, out int shell32Index))
                {
                    return GetShell32Icon(shell32Index);
                }

                if (!string.IsNullOrWhiteSpace(Executable) && File.Exists(Executable))
                {
                    using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(Executable))
                    {
                        if (icon != null)
                        {
                            var bmpSource = Imaging.CreateBitmapSourceFromHIcon(
                                icon.Handle,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromWidthAndHeight(56, 56));
                            bmpSource.Freeze();
                            return bmpSource;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public static bool TryGetShell32Index(string? value, out int index)
        {
            index = -1;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (value.StartsWith(Shell32IconPrefix, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(value[Shell32IconPrefix.Length..], out index) && index >= 0)
            {
                return true;
            }

            return BuiltInIconIndexes.TryGetValue(value, out index);
        }

        public static System.Windows.Media.ImageSource? GetShell32Icon(int index)
        {
            try
            {
                string shellPath = Path.Combine(Environment.SystemDirectory, "shell32.dll");
                IntPtr[] largeIcons = new IntPtr[1];
                if (ExtractIconEx(shellPath, index, largeIcons, null, 1) == 0 || largeIcons[0] == IntPtr.Zero)
                {
                    return null;
                }

                try
                {
                    var bmpSource = Imaging.CreateBitmapSourceFromHIcon(
                        largeIcons[0],
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromWidthAndHeight(56, 56));
                    bmpSource.Freeze();
                    return bmpSource;
                }
                finally
                {
                    DestroyIcon(largeIcons[0]);
                }
            }
            catch
            {
                return null;
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint ExtractIconEx(string szFileName, int nIconIndex, IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }

    public partial class MainWindow : Window
    {
        private bool _isAutoHideEnabled = true;
        private bool _isExpanded = true;
        private bool _isSnappedToTop = true;
        private bool _isDraggingWindow;
        private DispatcherTimer? _hoverCheckTimer;
        private DispatcherTimer? _hideDelayTimer;
        private double _dockedTop;
        private double _expandedTop;
        private const double RevealTabWidth = 30;
        private const double RevealTabHeight = 8;
        private const double TopSnapThreshold = 24;

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_NOACTIVATE = 3;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private ToolbarConfig _config = new();
        private string _configPath;
        private WinForms.NotifyIcon? _trayIcon;
        private WinForms.ToolStripMenuItem? _trayTopmostItem;
        private WinForms.ToolStripMenuItem? _trayArrangeItem;
        private bool _isArrangeMode;
        private LauncherItem? _draggedLauncher;
        private WpfPoint _dragStartPoint;

        public Visibility TextVisibility => _config.ToolbarSettings.DisplayMode == "IconsOnly" ? Visibility.Collapsed : Visibility.Visible;
        public string ToolbarTitle => _config.ToolbarSettings.TitleText;
        public Visibility ToolbarTitleVisibility => _config.ToolbarSettings.ShowTitle && !string.IsNullOrWhiteSpace(_config.ToolbarSettings.TitleText)
            ? Visibility.Visible
            : Visibility.Collapsed;

        public MainWindow()
        {
            InitializeComponent();
            SetupAutoHide();
            ApplyWindowsTheme();
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
            Icon = CreateApplicationIcon();

            string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SideBar");
            Directory.CreateDirectory(appDataDir);
            _configPath = Path.Combine(appDataDir, "toolbar_config.json");

            LoadConfiguration();
            PositionAtTop();
            SetupSystemTray();
            this.DataContext = this;
        }

        private void SetupAutoHide()
        {
            // Poll mouse cursor position to detect top-edge hover
            _hoverCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _hoverCheckTimer.Tick += (s, e) => CheckMouseProximity();
            _hoverCheckTimer.Start();

            // Half-second delay timer before hiding
            _hideDelayTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _hideDelayTimer.Tick += (s, e) =>
            {
                _hideDelayTimer.Stop();
                if (_isAutoHideEnabled && _isSnappedToTop && !_isDraggingWindow &&
                    !IsCursorInRevealZone() && !this.IsMouseOver)
                {
                    SlideWindow(false);
                }
            };
        }

        private void CheckMouseProximity()
        {
            if (!_isAutoHideEnabled || !_isSnappedToTop || _isArrangeMode || _isDraggingWindow) return;

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
            if (!_isSnappedToTop || _isExpanded)
            {
                return false;
            }

            var mousePosition = WinForms.Cursor.Position;
            WpfPoint localPosition = PointFromScreen(new WpfPoint(mousePosition.X, mousePosition.Y));
            double tabLeft = (ActualWidth - RevealTabWidth) / 2;

            return localPosition.X >= tabLeft &&
                   localPosition.X <= tabLeft + RevealTabWidth &&
                   localPosition.Y >= ActualHeight - RevealTabHeight &&
                   localPosition.Y <= ActualHeight;
        }

        private void SlideWindow(bool expand)
        {
            if (_isExpanded == expand || (!expand && !_isSnappedToTop)) return;
            _isExpanded = expand;
            RevealTab.Visibility = expand ? Visibility.Collapsed : Visibility.Visible;
            ToolbarSurface.IsHitTestVisible = expand;

            double targetTop = expand ? _expandedTop : _dockedTop;
            TimeSpan duration = TimeSpan.FromMilliseconds(200);

            DoubleAnimation animation = new DoubleAnimation
            {
                To = targetTop,
                Duration = duration,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            DoubleAnimation surfaceAnimation = new DoubleAnimation
            {
                To = expand ? 1 : 0,
                Duration = duration,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            this.BeginAnimation(Window.TopProperty, animation);
            ToolbarSurface.BeginAnimation(UIElement.OpacityProperty, surfaceAnimation);
        }

        private void ApplyWindowsTheme()
        {
            bool isLightTheme = true;
            try
            {
                using RegistryKey? personalizeKey = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                object? themeValue = personalizeKey?.GetValue("SystemUsesLightTheme") ??
                                     personalizeKey?.GetValue("AppsUseLightTheme");
                if (themeValue != null)
                {
                    isLightTheme = Convert.ToInt32(themeValue) != 0;
                }
            }
            catch
            {
                isLightTheme = false;
            }

            UpdateBrushColor("ToolbarBg", isLightTheme ? "F3F3F3" : "202020");
            UpdateBrushColor("ToolbarBorder", isLightTheme ? "D1D1D1" : "454545");
            UpdateBrushColor("ToolbarAccent", isLightTheme ? "0078D4" : "60CDFF");
            UpdateBrushColor("ToolbarText", isLightTheme ? "1A1A1A" : "F3F3F3");
            UpdateBrushColor("ButtonBg", isLightTheme ? "E9E9E9" : "2B2B2B");
            UpdateBrushColor("ButtonHover", isLightTheme ? "DADADA" : "3A3A3A");
            UpdateBrushColor("ButtonPressed", isLightTheme ? "C5C5C5" : "1C1C1C");
        }

        private void UpdateBrushColor(string resourceKey, string hexColor)
        {
            if (Resources.Contains(resourceKey))
            {
                System.Windows.Media.Color color = (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter.ConvertFromString("#" + hexColor);

                Resources[resourceKey] = new System.Windows.Media.SolidColorBrush(color);
            }
        }

        private void SystemEvents_UserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General)
            {
                Dispatcher.BeginInvoke(ApplyWindowsTheme);
            }
        }

        private void SetupSystemTray()
        {
            _trayIcon = new WinForms.NotifyIcon
            {
                Icon = CreateDynamicTrayIcon(),
                Text = "SideBar Desktop Toolbar",
                Visible = true
            };

            var trayMenu = new WinForms.ContextMenuStrip();

            _trayTopmostItem = new WinForms.ToolStripMenuItem("Always on Top", null, (s, e) => ToggleAlwaysOnTop())
            {
                Checked = _config.ToolbarSettings.AlwaysOnTop,
                CheckOnClick = true
            };
            trayMenu.Items.Add(_trayTopmostItem);

            _trayArrangeItem = new WinForms.ToolStripMenuItem("Arrange Icons", null, (s, e) => ToggleArrangeMode())
            {
                CheckOnClick = true
            };
            trayMenu.Items.Add(_trayArrangeItem);

            trayMenu.Items.Add(new WinForms.ToolStripSeparator());
            trayMenu.Items.Add("Reload Config", null, (s, e) => LoadConfiguration());
            trayMenu.Items.Add("Toolbar Settings...", null, (s, e) => ShowSettingsWindow());
            trayMenu.Items.Add("Open Config File", null, (s, e) => OpenConfigFile());
            trayMenu.Items.Add(new WinForms.ToolStripSeparator());
            trayMenu.Items.Add("Exit SideBar", null, (s, e) => System.Windows.Application.Current.Shutdown());

            _trayIcon.ContextMenuStrip = trayMenu;
            _trayIcon.DoubleClick += (s, e) => LoadConfiguration();
        }

        private void ToggleArrangeMode()
        {
            _isArrangeMode = _trayArrangeItem?.Checked == true;
            _draggedLauncher = null;
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

        private System.Windows.Media.ImageSource CreateApplicationIcon()
        {
            using var icon = CreateDynamicTrayIcon();
            var image = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(32, 32));
            image.Freeze();
            return image;
        }

        private System.Drawing.Icon CreateDynamicTrayIcon()
        {
            using (Bitmap bitmap = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                    g.Clear(Color.FromArgb(30, 30, 30));
                    using (Pen borderPen = new Pen(Color.FromArgb(0, 122, 204), 2))
                    {
                        g.DrawRectangle(borderPen, 1, 1, 30, 30);
                    }

                    using (Font font = new Font("Consolas", 11, System.Drawing.FontStyle.Bold))
                    {
                        using (Brush brush = new SolidBrush(Color.FromArgb(78, 201, 176)))
                        {
                            g.DrawString(">_", font, brush, 3, 6);
                        }
                    }
                }
                IntPtr hIcon = bitmap.GetHicon();
                try
                {
                    using var sourceIcon = System.Drawing.Icon.FromHandle(hIcon);
                    return (System.Drawing.Icon)sourceIcon.Clone();
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
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
                    _config.LauncherItems.Add(new LauncherItem { Label = "Workspace", Executable = "explorer.exe", Arguments = "C:\\ai" });
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

            SetAutoHideEnabled(_config.ToolbarSettings.AutoHideEnabled);

            this.Height = _config.ToolbarSettings.Height;
            this.Width = _config.ToolbarSettings.Width;
            this.Topmost = _config.ToolbarSettings.AlwaysOnTop;

            if (_trayTopmostItem != null)
            {
                _trayTopmostItem.Checked = _config.ToolbarSettings.AlwaysOnTop;
            }

            LauncherItemsControl.ItemsSource = _config.LauncherItems;

            if (MenuLockToggle != null)
            {
                MenuLockToggle.Header = _config.ToolbarSettings.IsLocked ? "🔓 Unlock Toolbar" : "🔒 Lock Toolbar";
            }
            if (DragHandleIcon != null)
            {
                DragHandleIcon.Cursor = System.Windows.Input.Cursors.Arrow;
                DragHandleIcon.Opacity = _config.ToolbarSettings.IsLocked ? 0.25 : 0.5;
            }

            this.DataContext = null;
            this.DataContext = this;
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
                System.Windows.MessageBox.Show(this, $"Failed to save config: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;

            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);

            HwndSource source = HwndSource.FromHwnd(hwnd);
            source.AddHook(WndProc);
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

        private void PositionAtTop()
        {
            double screenWidth = SystemParameters.WorkArea.Width;

            // Absolute top edge snapping (ignoring work area top offset so it sits flush at Y=0)
            _expandedTop = 0;
            _dockedTop = -this.Height + RevealTabHeight;
            _isSnappedToTop = true;
            _isExpanded = true;
            ToolbarSurface.BeginAnimation(UIElement.OpacityProperty, null);
            ToolbarSurface.Opacity = 1;
            ToolbarSurface.IsHitTestVisible = true;

            double leftOffset = SystemParameters.WorkArea.Left + (screenWidth - this.Width) / 2;

            this.Left = leftOffset;
            this.Top = _isExpanded ? _expandedTop : _dockedTop;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
        }

        protected override void OnClosed(EventArgs e)
        {
            _hoverCheckTimer?.Stop();
            _hideDelayTimer?.Stop();
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            base.OnClosed(e);
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && !_config.ToolbarSettings.IsLocked)
            {
                _isDraggingWindow = true;
                _hideDelayTimer?.Stop();
                try
                {
                    this.DragMove();
                }
                catch { }
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
                _expandedTop = 0;
                _dockedTop = -Height + RevealTabHeight;
                Top = _expandedTop;
            }
            else
            {
                _isSnappedToTop = false;
            }
        }

        private void Toolbar_DragOver(object sender, System.Windows.DragEventArgs e)
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

        private void Toolbar_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            DropHint.Visibility = Visibility.Collapsed;
        }

        private void Toolbar_Drop(object sender, WpfDragEventArgs e)
        {
            DropHint.Visibility = Visibility.Collapsed;

            if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) || e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] paths)
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
                LoadConfiguration();
            }

            e.Handled = true;
        }

        private void LauncherButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isArrangeMode)
            {
                return;
            }

            if (sender is FrameworkElement fe && fe.DataContext is LauncherItem item)
            {
                if (!string.IsNullOrWhiteSpace(item.Executable))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = item.Executable,
                            Arguments = item.Arguments,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show(this, $"Failed to launch {item.Label}: {ex.Message}", "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
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
            if (!_isArrangeMode || e.LeftButton != MouseButtonState.Pressed ||
                _draggedLauncher == null)
            {
                return;
            }

            System.Windows.Point currentPoint = e.GetPosition(null);
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
            LauncherItemsControl.ItemsSource = null;
            LauncherItemsControl.ItemsSource = _config.LauncherItems;
            e.Handled = true;
        }

        private void LauncherButton_DragOver(object sender, WpfDragEventArgs e)
        {
            if (_isArrangeMode &&
                _draggedLauncher != null &&
                e.Data.GetDataPresent(typeof(LauncherItem)))
            {
                e.Effects = System.Windows.DragDropEffects.Move;
                e.Handled = true;
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
        }

        private void Menu_Reload_Click(object sender, RoutedEventArgs e)
        {
            LoadConfiguration();
        }

        private void Menu_ToggleLock_Click(object sender, RoutedEventArgs e)
        {
            _config.ToolbarSettings.IsLocked = !_config.ToolbarSettings.IsLocked;
            SaveConfiguration();
            LoadConfiguration();
        }

        private void Menu_ToggleAutoHide_Click(object sender, RoutedEventArgs e)
        {
            SetAutoHideEnabled(MenuAutoHideToggle.IsChecked);
            SaveConfiguration();
        }

        private void SetAutoHideEnabled(bool isEnabled)
        {
            _isAutoHideEnabled = isEnabled;
            _config.ToolbarSettings.AutoHideEnabled = isEnabled;
            MenuAutoHideToggle.IsChecked = isEnabled;

            if (!isEnabled)
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
            LoadConfiguration();
        }

        private void Menu_ModeIconsOnly_Click(object sender, RoutedEventArgs e)
        {
            _config.ToolbarSettings.DisplayMode = "IconsOnly";
            SaveConfiguration();
            LoadConfiguration();
        }

        private void Menu_Close_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void Menu_EditConfig_Click(object sender, RoutedEventArgs e)
        {
            ShowSettingsWindow();
        }

        private void ShowSettingsWindow()
        {
            var settingsWindow = new SettingsWindow(_config.ToolbarSettings)
            {
                Owner = this
            };

            if (settingsWindow.ShowDialog() == true && settingsWindow.UpdatedSettings is ToolbarSettings updatedSettings)
            {
                _config.ToolbarSettings = updatedSettings;
                SaveConfiguration();
                LoadConfiguration();
                PositionAtTop();
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
                System.Windows.MessageBox.Show(this, $"Could not open config file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Menu_AddLauncher_Click(object sender, RoutedEventArgs e)
        {
            var newItem = new LauncherItem { Label = "New App", Executable = "cmd.exe", Arguments = "" };
            if (PromptEditLauncher("Add New Launcher", newItem))
            {
                _config.LauncherItems.Add(newItem);
                SaveConfiguration();
                LoadConfiguration();
            }
        }

        private void ItemMenu_Edit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem mi && mi.DataContext is LauncherItem item)
            {
                if (PromptEditLauncher("Edit Launcher", item))
                {
                    item.ClearIconCache();
                    SaveConfiguration();
                    LoadConfiguration();
                }
            }
        }

        private void ItemMenu_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem mi && mi.DataContext is LauncherItem item)
            {
                if (System.Windows.MessageBox.Show(this, $"Are you sure you want to remove '{item.Label}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _config.LauncherItems.Remove(item);
                    SaveConfiguration();
                    LoadConfiguration();
                }
            }
        }

        private bool PromptEditLauncher(string title, LauncherItem item)
        {
            Window dialog = new Window
            {
                Title = title,
                Width = 450,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Owner = this,
                Topmost = this.Topmost,
                ShowInTaskbar = false,
                Icon = this.Icon
            };

            StackPanel panel = new StackPanel { Margin = new Thickness(15) };

            panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "Label:", Margin = new Thickness(0, 0, 0, 4) });
            System.Windows.Controls.TextBox txtLabel = new System.Windows.Controls.TextBox { Text = item.Label, Margin = new Thickness(0, 0, 0, 10) };
            panel.Children.Add(txtLabel);

            panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "Executable Path / Command:", Margin = new Thickness(0, 0, 0, 4) });
            Grid execGrid = new Grid();
            execGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            execGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });

            System.Windows.Controls.TextBox txtExec = new System.Windows.Controls.TextBox { Text = item.Executable, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(txtExec, 0);
            execGrid.Children.Add(txtExec);

            System.Windows.Controls.Button btnBrowseExec = new System.Windows.Controls.Button { Content = "Browse...", Margin = new Thickness(6, 0, 0, 0) };
            Grid.SetColumn(btnBrowseExec, 1);
            btnBrowseExec.Click += (s, ev) =>
            {
                var ofd = new Microsoft.Win32.OpenFileDialog { Filter = "Executables (*.exe)|*.exe|All Files (*.*)|*.*" };
                if (ofd.ShowDialog() == true)
                {
                    txtExec.Text = ofd.FileName;
                    if (string.IsNullOrWhiteSpace(txtLabel.Text) || txtLabel.Text == "New App")
                    {
                        txtLabel.Text = System.IO.Path.GetFileNameWithoutExtension(ofd.FileName);
                    }
                }
            };
            execGrid.Children.Add(btnBrowseExec);
            panel.Children.Add(execGrid);

            panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "Custom Icon File (.ico / .png - Optional):", Margin = new Thickness(0, 10, 0, 4) });
            Grid iconGrid = new Grid();
            iconGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            iconGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });

            System.Windows.Controls.TextBox txtIcon = new System.Windows.Controls.TextBox { Text = item.IconPath, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(txtIcon, 0);
            iconGrid.Children.Add(txtIcon);

            System.Windows.Controls.Button btnBrowseIcon = new System.Windows.Controls.Button { Content = "Browse...", Margin = new Thickness(6, 0, 0, 0) };
            Grid.SetColumn(btnBrowseIcon, 1);
            btnBrowseIcon.Click += (s, ev) =>
            {
                var ofd = new Microsoft.Win32.OpenFileDialog { Filter = "Icon Files (*.ico;*.png)|*.ico;*.png|All Files (*.*)|*.*" };
                if (ofd.ShowDialog() == true)
                {
                    txtIcon.Text = ofd.FileName;
                }
            };
            iconGrid.Children.Add(btnBrowseIcon);
            panel.Children.Add(iconGrid);

            panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "Built-in Icon:", Margin = new Thickness(0, 10, 0, 4) });
            var iconChoices = new List<KeyValuePair<string, string>>
            {
                new("Use application icon", "")
            };
            foreach (string iconName in LauncherItem.BuiltInIconIndexes.Keys)
            {
                iconChoices.Add(new KeyValuePair<string, string>(iconName, iconName));
            }

            string selectedBuiltInIcon = item.BuiltInIcon;

            System.Windows.Controls.ComboBox cmbBuiltInIcon = new System.Windows.Controls.ComboBox
            {
                ItemsSource = iconChoices,
                DisplayMemberPath = "Key",
                SelectedValuePath = "Value",
                SelectedValue = LauncherItem.BuiltInIconIndexes.ContainsKey(item.BuiltInIcon) ? item.BuiltInIcon : "",
                Margin = new Thickness(0, 0, 0, 4)
            };
            cmbBuiltInIcon.SelectionChanged += (s, ev) =>
            {
                selectedBuiltInIcon = cmbBuiltInIcon.SelectedValue?.ToString() ?? "";
                txtIcon.Text = "";
            };
            panel.Children.Add(cmbBuiltInIcon);

            System.Windows.Controls.Button btnChooseShellIcon = new System.Windows.Controls.Button
            {
                Content = "Choose an icon from shell32.dll...",
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Margin = new Thickness(0, 4, 0, 0)
            };
            btnChooseShellIcon.Click += (s, ev) =>
            {
                string? chosenIcon = ShowShell32IconPicker();
                if (chosenIcon != null)
                {
                    cmbBuiltInIcon.SelectedValue = LauncherItem.BuiltInIconIndexes.ContainsKey(chosenIcon) ? chosenIcon : "";
                    selectedBuiltInIcon = chosenIcon;
                    txtIcon.Text = "";
                }
            };
            panel.Children.Add(btnChooseShellIcon);

            panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "Arguments:", Margin = new Thickness(0, 10, 0, 4) });
            System.Windows.Controls.TextBox txtArgs = new System.Windows.Controls.TextBox { Text = item.Arguments, Margin = new Thickness(0, 0, 0, 14) };
            panel.Children.Add(txtArgs);

            StackPanel btnPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
            System.Windows.Controls.Button btnSave = new System.Windows.Controls.Button { Content = "Save", Width = 75, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            System.Windows.Controls.Button btnCancel = new System.Windows.Controls.Button { Content = "Cancel", Width = 75, IsCancel = true };

            bool dialogResult = false;
            btnSave.Click += (s, ev) =>
            {
                item.Label = txtLabel.Text;
                item.Executable = txtExec.Text;
                item.IconPath = txtIcon.Text;
                item.BuiltInIcon = selectedBuiltInIcon;
                item.Arguments = txtArgs.Text;
                dialogResult = true;
                dialog.Close();
            };

            btnPanel.Children.Add(btnSave);
            btnPanel.Children.Add(btnCancel);
            panel.Children.Add(btnPanel);

            dialog.Content = new System.Windows.Controls.ScrollViewer
            {
                Content = panel,
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled
            };
            dialog.ShowDialog();

            return dialogResult;
        }

        private string? ShowShell32IconPicker()
        {
            Window picker = new Window
            {
                Title = "Choose an icon from shell32.dll",
                Width = 620,
                Height = 470,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.CanResize,
                Topmost = this.Topmost,
                ShowInTaskbar = false,
                Icon = this.Icon
            };

            string? selectedIcon = null;
            var iconPanel = new System.Windows.Controls.WrapPanel { Margin = new Thickness(10) };
            var scrollViewer = new System.Windows.Controls.ScrollViewer
            {
                Content = iconPanel,
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled
            };

            for (int index = 0; index < 256; index++)
            {
                var icon = LauncherItem.GetShell32Icon(index);
                if (icon == null)
                {
                    continue;
                }

                int selectedIndex = index;
                var iconButton = new System.Windows.Controls.Button
                {
                    Width = 82,
                    Height = 78,
                    Margin = new Thickness(3),
                    Padding = new Thickness(3),
                    ToolTip = $"shell32.dll, index {index}"
                };
                var buttonContent = new System.Windows.Controls.StackPanel
                {
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                };
                buttonContent.Children.Add(new System.Windows.Controls.Image
                {
                    Source = icon,
                    Width = 36,
                    Height = 36,
                    Margin = new Thickness(0, 2, 0, 2)
                });
                buttonContent.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = $"#{index}",
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                });
                iconButton.Content = buttonContent;
                iconButton.Click += (s, ev) =>
                {
                    selectedIcon = LauncherItem.Shell32IconPrefix + selectedIndex;
                    picker.Close();
                };
                iconPanel.Children.Add(iconButton);
            }

            picker.Content = scrollViewer;
            picker.ShowDialog();
            return selectedIcon;
        }
    }
}