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
using System.Windows.Media.Imaging;
using WinForms = System.Windows.Forms;

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
        public double BottomOffset { get; set; } = 12;
        public double CornerRadius { get; set; } = 6;
        public string DisplayMode { get; set; } = "IconsAndText";
        public bool IsLocked { get; set; } = false;
        public bool AlwaysOnTop { get; set; } = true;
    }

    public class LauncherItem
    {
        public string Label { get; set; } = "";
        public string Executable { get; set; } = "";
        public string Arguments { get; set; } = "";
        public string IconPath { get; set; } = "";

        [System.Text.Json.Serialization.JsonIgnore]
        public System.Windows.Media.ImageSource? AppIcon => ResolveAppIcon();

        private System.Windows.Media.ImageSource? ResolveAppIcon()
        {
            try
            {
                // 1. Try custom icon file (.ico or .png) if specified
                if (!string.IsNullOrWhiteSpace(IconPath) && File.Exists(IconPath))
                {
                    if (IconPath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var icon = new System.Drawing.Icon(IconPath))
                        {
                            return Imaging.CreateBitmapSourceFromHIcon(
                                icon.Handle,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                        }
                    }
                    else
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(IconPath);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        return bitmap;
                    }
                }

                // 2. Fall back to extracting icon from executable using Shell32 for reliability
                if (!string.IsNullOrWhiteSpace(Executable) && File.Exists(Executable))
                {
                    using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(Executable))
                    {
                        if (icon != null)
                        {
                            return Imaging.CreateBitmapSourceFromHIcon(
                                icon.Handle,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                        }
                    }
                }
            }
            catch { }
            return null;
        }
    }

    public partial class MainWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_NOACTIVATE = 3;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private ToolbarConfig _config = new();
        private string _configPath;
        private WinForms.NotifyIcon? _trayIcon;
        private WinForms.ToolStripMenuItem? _trayTopmostItem;

        public Visibility TextVisibility => _config.ToolbarSettings.DisplayMode == "IconsOnly" ? Visibility.Collapsed : Visibility.Visible;

        public MainWindow()
        {
            InitializeComponent();

            string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SideBar");
            Directory.CreateDirectory(appDataDir);
            _configPath = Path.Combine(appDataDir, "toolbar_config.json");

            LoadConfiguration();
            PositionAtBottom();
            SetupSystemTray();
            this.DataContext = this;
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

            trayMenu.Items.Add(new WinForms.ToolStripSeparator());
            trayMenu.Items.Add("Reload Config", null, (s, e) => LoadConfiguration());
            trayMenu.Items.Add("Open Settings File", null, (s, e) => OpenConfigFile());
            trayMenu.Items.Add(new WinForms.ToolStripSeparator());
            trayMenu.Items.Add("Exit SideBar", null, (s, e) => System.Windows.Application.Current.Shutdown());

            _trayIcon.ContextMenuStrip = trayMenu;
            _trayIcon.DoubleClick += (s, e) => LoadConfiguration();
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
                return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
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

        private void PositionAtBottom()
        {
            double screenWidth = SystemParameters.WorkArea.Width;
            double screenHeight = SystemParameters.WorkArea.Height;
            double topOffset = SystemParameters.WorkArea.Top + screenHeight - this.Height - _config.ToolbarSettings.BottomOffset;
            double leftOffset = SystemParameters.WorkArea.Left + (screenWidth - this.Width) / 2;

            this.Left = leftOffset;
            this.Top = topOffset;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
        }

        protected override void OnClosed(EventArgs e)
        {
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
                try
                {
                    this.DragMove();
                }
                catch { }
            }
        }

        private void LauncherButton_Click(object sender, RoutedEventArgs e)
        {
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
            OpenConfigFile();
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
                Height = 330,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize
            };

            StackPanel panel = new StackPanel { Margin = new Thickness(15) };

            // Label Input
            panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "Label:", Margin = new Thickness(0, 0, 0, 4) });
            System.Windows.Controls.TextBox txtLabel = new System.Windows.Controls.TextBox { Text = item.Label, Margin = new Thickness(0, 0, 0, 10) };
            panel.Children.Add(txtLabel);

            // Executable Path Input + Browse Button
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

            // Custom Icon File Path Input + Browse Button (Optional override)
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

            // Arguments Input
            panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "Arguments:", Margin = new Thickness(0, 10, 0, 4) });
            System.Windows.Controls.TextBox txtArgs = new System.Windows.Controls.TextBox { Text = item.Arguments, Margin = new Thickness(0, 0, 0, 14) };
            panel.Children.Add(txtArgs);

            // Save / Cancel Buttons
            StackPanel btnPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
            System.Windows.Controls.Button btnSave = new System.Windows.Controls.Button { Content = "Save", Width = 75, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            System.Windows.Controls.Button btnCancel = new System.Windows.Controls.Button { Content = "Cancel", Width = 75, IsCancel = true };

            bool dialogResult = false;
            btnSave.Click += (s, ev) =>
            {
                item.Label = txtLabel.Text;
                item.Executable = txtExec.Text;
                item.IconPath = txtIcon.Text;
                item.Arguments = txtArgs.Text;
                dialogResult = true;
                dialog.Close();
            };

            btnPanel.Children.Add(btnSave);
            btnPanel.Children.Add(btnCancel);
            panel.Children.Add(btnPanel);

            dialog.Content = panel;
            dialog.ShowDialog();

            return dialogResult;
        }
    }
}