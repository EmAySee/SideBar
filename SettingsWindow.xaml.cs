using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace DesktopToolbar
{
    public partial class SettingsWindow : Window
    {
        private readonly ToolbarSettings _currentSettings;
        public ToolbarSettings? UpdatedSettings { get; private set; }

        public SettingsWindow(ToolbarSettings settings)
        {
            InitializeComponent();
            ApplyWindowsTheme();
            _currentSettings = settings;

            TitleTextBox.Text = settings.TitleText ?? "LP:";
            ShowTitleCheckBox.IsChecked = settings.ShowTitle;
            DisplayModeComboBox.SelectedIndex = settings.DisplayMode == "IconsOnly" ? 1 : 0;
            BottomOffsetTextBox.Text = settings.BottomOffset.ToString(CultureInfo.CurrentCulture);
            AlwaysOnTopCheckBox.IsChecked = settings.AlwaysOnTop;
            IsLockedCheckBox.IsChecked = settings.IsLocked;
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

            Resources["WindowBg"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isLightTheme ? "#F9F9F9" : "#202020"));
            Resources["WindowBorder"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isLightTheme ? "#E0E0E0" : "#333333"));
            Resources["TextColor"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isLightTheme ? "#1A1A1A" : "#F3F3F3"));
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(BottomOffsetTextBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double bottomOffset) ||
                double.IsNaN(bottomOffset) || double.IsInfinity(bottomOffset))
            {
                System.Windows.MessageBox.Show(this, "Enter a valid bottom offset.", "Invalid Setting", MessageBoxButton.OK, MessageBoxImage.Warning);
                BottomOffsetTextBox.Focus();
                BottomOffsetTextBox.SelectAll();
                return;
            }

            UpdatedSettings = new ToolbarSettings
            {
                Height = _currentSettings.Height,
                Width = _currentSettings.Width,
                BottomOffset = bottomOffset,
                CornerRadius = _currentSettings.CornerRadius,
                DisplayMode = DisplayModeComboBox.SelectedIndex == 1 ? "IconsOnly" : "IconsAndText",
                IsLocked = IsLockedCheckBox.IsChecked == true,
                AlwaysOnTop = AlwaysOnTopCheckBox.IsChecked == true,
                TitleText = TitleTextBox.Text,
                ShowTitle = ShowTitleCheckBox.IsChecked == true
            };

            DialogResult = true;
        }
    }
}