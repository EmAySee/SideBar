using System;
using System.Reflection;
using System.Windows;
using DesktopToolbar.Services;

namespace DesktopToolbar
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            WindowHelper.ApplyWindows11Chrome(this, ThemeManager.IsDarkTheme);

            AppLogoImage.Source = IconService.GetAppIconImage(168);

            Version? version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"Version {version?.ToString(3) ?? "Unknown"}";
        }
    }
}
