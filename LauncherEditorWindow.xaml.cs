using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using DesktopToolbar.Services;

namespace DesktopToolbar
{
    public partial class LauncherEditorWindow : Window
    {
        private readonly LauncherItem _item;
        private string _selectedBuiltInIcon;
        private bool _suppressEvents;

        public LauncherEditorWindow(string title, LauncherItem item)
        {
            InitializeComponent();
            WindowHelper.ApplyWindows11Chrome(this, ThemeManager.IsDarkTheme);

            _item = item;
            _selectedBuiltInIcon = item.BuiltInIcon ?? "";

            Title = title;
            HeaderTitleText.Text = title;

            var iconChoices = new List<KeyValuePair<string, string>>
            {
                new("Use application / folder icon", "")
            };
            foreach (string iconName in LauncherItem.BuiltInIconIndexes.Keys)
            {
                iconChoices.Add(new KeyValuePair<string, string>(iconName, iconName));
            }

            _suppressEvents = true;
            LabelTextBox.Text = item.Label;
            ExecutableTextBox.Text = item.Executable;
            ArgumentsTextBox.Text = item.Arguments;
            IconPathTextBox.Text = item.IconPath;
            BuiltInIconComboBox.ItemsSource = iconChoices;
            BuiltInIconComboBox.SelectedValue = LauncherItem.BuiltInIconIndexes.ContainsKey(_selectedBuiltInIcon)
                ? _selectedBuiltInIcon
                : "";
            _suppressEvents = false;

            UpdateIconPreview();
        }

        private void UpdateIconPreview()
        {
            var previewItem = new LauncherItem
            {
                Executable = ExecutableTextBox.Text,
                IconPath = IconPathTextBox.Text,
                BuiltInIcon = _selectedBuiltInIcon
            };
            IconPreview.Source = previewItem.AppIcon;
        }

        private void IconInput_Changed(object sender, TextChangedEventArgs e)
        {
            if (_suppressEvents) return;
            UpdateIconPreview();
        }

        private void BuiltInIconComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;

            _selectedBuiltInIcon = BuiltInIconComboBox.SelectedValue?.ToString() ?? "";
            if (!string.IsNullOrEmpty(_selectedBuiltInIcon))
            {
                _suppressEvents = true;
                IconPathTextBox.Text = "";
                _suppressEvents = false;
            }
            UpdateIconPreview();
        }

        private void BrowseExecutable_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executables (*.exe;*.bat;*.cmd;*.lnk)|*.exe;*.bat;*.cmd;*.lnk|All Files (*.*)|*.*"
            };
            if (ofd.ShowDialog(this) == true)
            {
                ExecutableTextBox.Text = ofd.FileName;
                if (string.IsNullOrWhiteSpace(LabelTextBox.Text) ||
                    LabelTextBox.Text == "New App" ||
                    LabelTextBox.Text == "New Shortcut")
                {
                    LabelTextBox.Text = Path.GetFileNameWithoutExtension(ofd.FileName);
                }
                UpdateIconPreview();
            }
        }

        private void BrowseIcon_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Icon & Image Files (*.ico;*.png;*.jpg;*.jpeg)|*.ico;*.png;*.jpg;*.jpeg|All Files (*.*)|*.*"
            };
            if (ofd.ShowDialog(this) == true)
            {
                _suppressEvents = true;
                IconPathTextBox.Text = ofd.FileName;
                _selectedBuiltInIcon = "";
                BuiltInIconComboBox.SelectedValue = "";
                _suppressEvents = false;
                UpdateIconPreview();
            }
        }

        private void ChooseShellIcon_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Shell32IconPickerWindow
            {
                Owner = this,
                Topmost = this.Topmost,
                Icon = this.Icon
            };

            if (picker.ShowDialog() == true && picker.SelectedIcon != null)
            {
                _suppressEvents = true;
                _selectedBuiltInIcon = picker.SelectedIcon;
                BuiltInIconComboBox.SelectedValue = LauncherItem.BuiltInIconIndexes.ContainsKey(_selectedBuiltInIcon)
                    ? _selectedBuiltInIcon
                    : "";
                IconPathTextBox.Text = "";
                _suppressEvents = false;
                UpdateIconPreview();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ExecutableTextBox.Text))
            {
                System.Windows.MessageBox.Show(this, "Please enter an executable path, folder, or command.",
                    "Missing Target", MessageBoxButton.OK, MessageBoxImage.Warning);
                ExecutableTextBox.Focus();
                return;
            }

            _item.Label = string.IsNullOrWhiteSpace(LabelTextBox.Text)
                ? Path.GetFileNameWithoutExtension(ExecutableTextBox.Text.Trim())
                : LabelTextBox.Text.Trim();
            _item.Executable = ExecutableTextBox.Text.Trim();
            _item.Arguments = ArgumentsTextBox.Text;
            _item.IconPath = IconPathTextBox.Text.Trim();
            _item.BuiltInIcon = _selectedBuiltInIcon;
            _item.ClearIconCache();

            DialogResult = true;
        }
    }
}

