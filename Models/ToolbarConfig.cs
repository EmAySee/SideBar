using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopToolbar
{
    public class ToolbarConfig
    {
        public ToolbarSettings ToolbarSettings { get; set; } = new();
        public List<LauncherItem> LauncherItems { get; set; } = new();
    }

    public class ToolbarSettings
    {
        public double Height { get; set; } = 54;
        public double Width { get; set; } = 750;
        public double BottomOffset { get; set; } = 0;
        public double CornerRadius { get; set; } = 12;
        public string DisplayMode { get; set; } = "IconsAndText";
        public string ThemeMode { get; set; } = "System"; // "System", "Light", "Dark"
        public bool IsLocked { get; set; } = false;
        public bool AlwaysOnTop { get; set; } = true;
        public bool AutoHideEnabled { get; set; } = true;
        public string TitleText { get; set; } = "PhantomCore SideBar";
        public bool ShowTitle { get; set; } = false;
    }

    public class LauncherItem
    {
        public const string Shell32IconPrefix = "Shell32:";

        public static readonly IReadOnlyDictionary<string, int> BuiltInIconIndexes = new Dictionary<string, int>
        {
            ["Application"] = 2,
            ["Folder"] = 3,
            ["Folder Open"] = 4,
            ["Computer"] = 15,
            ["Search"] = 22,
            ["Network"] = 17,
            ["Control Panel"] = 21,
            ["Document"] = 1,
            ["Warning"] = 78
        };

        public string Label { get; set; } = "";
        public string Executable { get; set; } = "";
        public string Arguments { get; set; } = "";
        public string IconPath { get; set; } = "";
        public string BuiltInIcon { get; set; } = "";

        private ImageSource? _cachedIcon;
        private bool _iconResolved;

        [JsonIgnore]
        public ImageSource? AppIcon
        {
            get
            {
                if (!_iconResolved)
                {
                    _cachedIcon = ResolveAppIcon();
                    _iconResolved = true;
                }
                return _cachedIcon;
            }
        }

        public void ClearIconCache()
        {
            _cachedIcon = null;
            _iconResolved = false;
        }

        public LauncherItem Clone()
        {
            return new LauncherItem
            {
                Label = Label,
                Executable = Executable,
                Arguments = Arguments,
                IconPath = IconPath,
                BuiltInIcon = BuiltInIcon
            };
        }

        private ImageSource? ResolveAppIcon()
        {
            try
            {
                // 1. Custom icon file (.ico / .png / etc.)
                if (!string.IsNullOrWhiteSpace(IconPath) && File.Exists(IconPath))
                {
                    if (IconPath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                    {
                        using var icon = new System.Drawing.Icon(IconPath, 64, 64);
                        return ConvertHIconToImageSource(icon.Handle);
                    }

                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(IconPath, UriKind.Absolute);
                    bitmap.DecodePixelWidth = 64;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }

                // 2. Explicit built-in / shell32 icon
                if (TryGetShell32Index(BuiltInIcon, out int shell32Index))
                {
                    return GetShell32Icon(shell32Index);
                }

                // 3. Directory shortcut -> system folder icon
                if (!string.IsNullOrWhiteSpace(Executable) && Directory.Exists(Executable))
                {
                    return GetShell32Icon(3);
                }

                // 4. Executable file (direct path or resolved from PATH)
                string? resolvedPath = ResolveExecutablePath(Executable);
                if (!string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath))
                {
                    using var icon = System.Drawing.Icon.ExtractAssociatedIcon(resolvedPath);
                    if (icon != null)
                    {
                        return ConvertHIconToImageSource(icon.Handle);
                    }
                }

                // 5. Fallback application icon so tiles are never blank
                return GetShell32Icon(2);
            }
            catch
            {
                return GetShell32Icon(2);
            }
        }

        private static string? ResolveExecutablePath(string? executable)
        {
            if (string.IsNullOrWhiteSpace(executable))
            {
                return null;
            }

            string trimmed = executable.Trim().Trim('"');
            if (File.Exists(trimmed))
            {
                return trimmed;
            }

            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(pathEnv))
            {
                return null;
            }

            string[] extensions = Path.HasExtension(trimmed)
                ? new[] { "" }
                : new[] { ".exe", ".com", ".bat", ".cmd" };

            foreach (string dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string cleanDir = dir.Trim().Trim('"');
                    foreach (string ext in extensions)
                    {
                        string candidate = Path.Combine(cleanDir, trimmed + ext);
                        if (File.Exists(candidate))
                        {
                            return candidate;
                        }
                    }
                }
                catch
                {
                    // Ignore malformed PATH entries
                }
            }

            return null;
        }

        private static ImageSource ConvertHIconToImageSource(IntPtr hIcon)
        {
            var bmpSource = Imaging.CreateBitmapSourceFromHIcon(
                hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(64, 64));
            bmpSource.Freeze();
            return bmpSource;
        }

        public static bool TryGetShell32Index(string? value, out int index)
        {
            index = -1;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (value.StartsWith(Shell32IconPrefix, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(value[Shell32IconPrefix.Length..], out index) && index >= 0)
            {
                return true;
            }

            return BuiltInIconIndexes.TryGetValue(value, out index);
        }

        public static ImageSource? GetShell32Icon(int index)
        {
            try
            {
                string shellPath = Path.Combine(Environment.SystemDirectory, "shell32.dll");
                IntPtr[] largeIcons = new IntPtr[1];
                if (ExtractIconEx(shellPath, index, largeIcons, null, 1) == 0 || largeIcons[0] == IntPtr.Zero)
                {
                    return null;
                }

                try
                {
                    return ConvertHIconToImageSource(largeIcons[0]);
                }
                finally
                {
                    DestroyIcon(largeIcons[0]);
                }
            }
            catch
            {
                return null;
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint ExtractIconEx(string szFileName, int nIconIndex, IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}

