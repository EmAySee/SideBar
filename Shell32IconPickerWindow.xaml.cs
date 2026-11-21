using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using DesktopToolbar.Services;

namespace DesktopToolbar
{
    public partial class Shell32IconPickerWindow : Window
    {
        public sealed class ShellIconEntry
        {
            public int Index { get; init; }
            public required ImageSource Image { get; init; }
            public string Label => $"#{Index}";
            public string ToolTip => $"shell32.dll, index {Index}";
        }

        public string? SelectedIcon { get; private set; }

        public Shell32IconPickerWindow()
        {
            InitializeComponent();
            WindowHelper.ApplyWindows11Chrome(this, ThemeManager.IsDarkTheme);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var entries = await Task.Run(() =>
            {
                var list = new List<ShellIconEntry>(256);
                for (int index = 0; index < 256; index++)
                {
                    var icon = LauncherItem.GetShell32Icon(index);
                    if (icon != null)
                    {
                        list.Add(new ShellIconEntry { Index = index, Image = icon });
                    }
                }
                return list;
            });

            IconsItemsControl.ItemsSource = entries;
            StatusText.Text = $"Select an icon from shell32.dll ({entries.Count} icons available)";
        }

        private void IconButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ShellIconEntry entry)
            {
                SelectedIcon = LauncherItem.Shell32IconPrefix + entry.Index;
                DialogResult = true;
            }
        }
    }
}

