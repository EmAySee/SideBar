using System.Reflection;
using System.Windows;

namespace DesktopToolbar
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();

            Version? version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"Version {version?.ToString(3) ?? "Unknown"}";
        }
    }
}
