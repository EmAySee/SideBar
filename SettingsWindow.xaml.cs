using System;
using System.Windows;
using System.Windows.Controls;
using DesktopToolbar.Services;

namespace DesktopToolbar
{
    public partial class SettingsWindow : Window
    {
        private readonly ToolbarSettings _currentSettings;
        public ToolbarSettings? UpdatedSettings { get; private set; }

        public SettingsWindow(ToolbarSettings settings)
        {
            InitializeComponent();
            WindowHelper.ApplyWindows11Chrome(this, ThemeManager.IsDarkTheme);

            _currentSettings = settings;

            TitleTextBox.Text = settings.TitleText ?? "PhantomCore SideBar";
            ShowTitleCheckBox.IsChecked = settings.ShowTitle;
            DisplayModeComboBox.SelectedIndex = settings.DisplayMode == "IconsOnly" ? 1 : 0;
            ThemeModeComboBox.SelectedIndex = settings.ThemeMode?.ToLowerInvariant() switch
            {
                "dark" => 1,
                "light" => 2,
                _ => 0
            };
            AlwaysOnTopCheckBox.IsChecked = settings.AlwaysOnTop;
            AutoHideCheckBox.IsChecked = settings.AutoHideEnabled;
            IsLockedCheckBox.IsChecked = settings.IsLocked;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string selectedTheme = (ThemeModeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "System";

            UpdatedSettings = new ToolbarSettings
            {
                Height = _currentSettings.Height,
                Width = _currentSettings.Width,
                BottomOffset = _currentSettings.BottomOffset,
                CornerRadius = _currentSettings.CornerRadius,
                DisplayMode = DisplayModeComboBox.SelectedIndex == 1 ? "IconsOnly" : "IconsAndText",
                ThemeMode = selectedTheme,
                IsLocked = IsLockedCheckBox.IsChecked == true,
                AlwaysOnTop = AlwaysOnTopCheckBox.IsChecked == true,
                AutoHideEnabled = AutoHideCheckBox.IsChecked == true,
                TitleText = TitleTextBox.Text,
                ShowTitle = ShowTitleCheckBox.IsChecked == true
            };

            DialogResult = true;
        }
    }
}