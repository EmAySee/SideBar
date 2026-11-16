using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace DesktopToolbar
{
    public partial class ShortcutManagerWindow : Window
    {
        private readonly List<LauncherItem> _items = new();
        private readonly Func<Window, string, LauncherItem, bool> _editLauncher;

        public List<LauncherItem>? UpdatedItems { get; private set; }

        public ShortcutManagerWindow(
            IEnumerable<LauncherItem> launcherItems,
            Func<Window, string, LauncherItem, bool> editLauncher)
        {
            InitializeComponent();
            _editLauncher = editLauncher;

            foreach (LauncherItem item in launcherItems)
            {
                _items.Add(Clone(item));
            }

            RefreshList(_items.Count > 0 ? _items[0] : null);
        }

        private static LauncherItem Clone(LauncherItem item)
        {
            return new LauncherItem
            {
                Label = item.Label,
                Executable = item.Executable,
                Arguments = item.Arguments,
                IconPath = item.IconPath,
                BuiltInIcon = item.BuiltInIcon
            };
        }

        private void RefreshList(LauncherItem? selectedItem)
        {
            ShortcutListBox.ItemsSource = null;
            ShortcutListBox.ItemsSource = _items;
            ShortcutListBox.SelectedItem = selectedItem;
        }

        private void ShortcutListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            int selectedIndex = ShortcutListBox.SelectedIndex;
            bool hasSelection = selectedIndex >= 0;
            EditButton.IsEnabled = hasSelection;
            DeleteButton.IsEnabled = hasSelection;
            MoveUpButton.IsEnabled = selectedIndex > 0;
            MoveDownButton.IsEnabled = selectedIndex >= 0 && selectedIndex < _items.Count - 1;

            if (ShortcutListBox.SelectedItem is LauncherItem item)
            {
                SelectedLabelText.Text = item.Label;
                SelectedExecutableText.Text = item.Executable;
            }
            else
            {
                SelectedLabelText.Text = "";
                SelectedExecutableText.Text = "";
            }
        }

        private void MoveUpButton_Click(object sender, RoutedEventArgs e)
        {
            MoveSelectedItem(-1);
        }

        private void MoveDownButton_Click(object sender, RoutedEventArgs e)
        {
            MoveSelectedItem(1);
        }

        private void MoveSelectedItem(int offset)
        {
            int currentIndex = ShortcutListBox.SelectedIndex;
            int newIndex = currentIndex + offset;
            if (currentIndex < 0 || newIndex < 0 || newIndex >= _items.Count)
            {
                return;
            }

            LauncherItem selectedItem = _items[currentIndex];
            _items.RemoveAt(currentIndex);
            _items.Insert(newIndex, selectedItem);
            RefreshList(selectedItem);
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var item = new LauncherItem { Label = "New Shortcut" };
            if (_editLauncher(this, "Add Shortcut", item))
            {
                _items.Add(item);
                RefreshList(item);
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (ShortcutListBox.SelectedItem is LauncherItem item &&
                _editLauncher(this, "Edit Shortcut", item))
            {
                RefreshList(item);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (ShortcutListBox.SelectedItem is not LauncherItem item)
            {
                return;
            }

            int index = _items.IndexOf(item);
            _items.Remove(item);
            LauncherItem? nextItem = _items.Count == 0 ? null : _items[Math.Min(index, _items.Count - 1)];
            RefreshList(nextItem);
        }

        private void ShortcutListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (EditButton.IsEnabled)
            {
                EditButton_Click(sender, e);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            UpdatedItems = _items;
            DialogResult = true;
        }
    }
}
