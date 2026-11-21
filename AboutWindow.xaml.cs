using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using DesktopToolbar.Services;

namespace DesktopToolbar
{
    public partial class AboutWindow : Window
    {
        private readonly bool _isSplashMode;
        private readonly TaskCompletionSource<bool> _splashCompleted = new();

        public Task SplashCompletedTask => _splashCompleted.Task;

        public AboutWindow(bool isSplashMode = false)
        {
            InitializeComponent();
            _isSplashMode = isSplashMode;

            AppLogoImage.Source = IconService.GetAppIconImage(192);

            Version? version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"Version {version?.ToString(3) ?? "Unknown"}";

            if (_isSplashMode)
            {
                Title = "PhantomCore SideBar";
                Topmost = true;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                SplashLoaderPanel.Visibility = Visibility.Visible;
                AboutFooterPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                SplashLoaderPanel.Visibility = Visibility.Collapsed;
                AboutFooterPanel.Visibility = Visibility.Visible;
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Smooth fade-in
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(OpacityProperty, fadeIn);

            if (!_isSplashMode)
            {
                return;
            }

            try
            {
                // Animate the gradient progress bar smoothly across 3.0 seconds
                double trackWidth = SplashLoaderPanel.ActualWidth > 0 ? SplashLoaderPanel.ActualWidth : 332;
                var progressAnim = new DoubleAnimation(0, trackWidth, TimeSpan.FromSeconds(2.75))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                };
                ProgressFill.BeginAnimation(WidthProperty, progressAnim);

                await Task.Delay(950);
                SplashStatusText.Text = "Loading launchers & Windows 11 theme...";

                await Task.Delay(1100);
                SplashStatusText.Text = "Docking to workspace...";

                await Task.Delay(700);

                // Smooth fade-out completing at ~3.0 seconds total
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(250))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                BeginAnimation(OpacityProperty, fadeOut);
                await Task.Delay(250);
            }
            finally
            {
                _splashCompleted.TrySetResult(true);
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isSplashMode && e.ButtonState == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
            }
        }
    }
}
