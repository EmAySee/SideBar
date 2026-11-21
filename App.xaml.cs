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

        protected override void OnStartup(StartupEventArgs e)
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

            ThemeManager.Initialize();

            MainWindow = new MainWindow();
            MainWindow.Show();
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