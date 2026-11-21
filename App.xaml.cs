using System;
using System.Threading;
using System.Windows;
using DesktopToolbar;
using DesktopToolbar.Services;

namespace SideBar
{
    public partial class App : System.Windows.Application
    {
        private Mutex? _singleInstanceMutex;
        private bool _ownsMutex;

        protected override async void OnStartup(StartupEventArgs e)
        {
            _singleInstanceMutex = new Mutex(true, "SideBar.SingleInstance", out bool createdNew);
            _ownsMutex = createdNew;

            if (!createdNew)
            {
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                Shutdown();
                return;
            }

            base.OnStartup(e);

            ShutdownMode = ShutdownMode.OnMainWindowClose;
            ThemeManager.Initialize();

            // Initialize MainWindow first so saved theme/config is applied before showing the splash screen
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;

            var splash = new AboutWindow(isSplashMode: true)
            {
                Icon = mainWindow.Icon
            };
            splash.Show();

            await splash.SplashCompletedTask;

            mainWindow.Show();
            splash.Close();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ThemeManager.Shutdown();

            if (_singleInstanceMutex != null)
            {
                if (_ownsMutex)
                {
                    try { _singleInstanceMutex.ReleaseMutex(); } catch (ApplicationException) { }
                }
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }

            base.OnExit(e);
        }
    }
}