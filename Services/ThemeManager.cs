using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;

namespace DesktopToolbar.Services
{
    public static class ThemeManager
    {
        private static string _themeMode = "System";
        private static bool _isInitialized;

        public static bool IsDarkTheme { get; private set; } = true;
        public static string ThemeMode => _themeMode;

        public static event EventHandler? ThemeChanged;

        public static void Initialize(string? themeMode = "System")
        {
            _themeMode = NormalizeMode(themeMode);

            if (!_isInitialized)
            {
                SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
                _isInitialized = true;
            }

            ApplyTheme();
        }

        public static void Shutdown()
        {
            if (_isInitialized)
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                _isInitialized = false;
            }
        }

        public static void SetThemeMode(string? themeMode)
        {
            string normalized = NormalizeMode(themeMode);
            if (_themeMode != normalized)
            {
                _themeMode = normalized;
            }
            ApplyTheme();
        }

        public static bool IsSystemLightTheme()
        {
            try
            {
                using RegistryKey? personalizeKey = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                object? themeValue = personalizeKey?.GetValue("AppsUseLightTheme") ??
                                     personalizeKey?.GetValue("SystemUsesLightTheme");
                if (themeValue != null)
                {
                    return Convert.ToInt32(themeValue) != 0;
                }
            }
            catch
            {
                // Default to dark if registry cannot be read
            }

            return false;
        }

        public static void ApplyTheme()
        {
            bool isDark = _themeMode switch
            {
                "Dark" => true,
                "Light" => false,
                _ => !IsSystemLightTheme()
            };

            IsDarkTheme = isDark;

            var app = System.Windows.Application.Current;
            if (app == null) return;

            var res = app.Resources;

            if (isDark)
            {
                // Windows 11 Fluent Dark Palette
                SetBrush(res, "WindowBgBrush", "#202020");
                SetBrush(res, "SurfaceBgBrush", "#E8202020");
                SetBrush(res, "CardBgBrush", "#2B2B2B");
                SetBrush(res, "CardElevatedBgBrush", "#323232");
                SetBrush(res, "SurfaceBorderBrush", "#2BFFFFFF");
                SetBrush(res, "ControlBorderBrush", "#1FFFFFFF");
                SetBrush(res, "ControlBgBrush", "#12FFFFFF");
                SetBrush(res, "ControlHoverBrush", "#1FFFFFFF");
                SetBrush(res, "ControlPressedBrush", "#0AFFFFFF");
                SetBrush(res, "InputBgBrush", "#2B2B2B");
                SetBrush(res, "InputHoverBgBrush", "#333333");
                SetBrush(res, "InputFocusBgBrush", "#1F1F1F");
                SetBrush(res, "TextPrimaryBrush", "#FFFFFF");
                SetBrush(res, "TextSecondaryBrush", "#C5C5C5");
                SetBrush(res, "TextDisabledBrush", "#777777");
                SetBrush(res, "AccentBrush", "#60CDFF");
                SetBrush(res, "AccentHoverBrush", "#7AD6FF");
                SetBrush(res, "AccentPressedBrush", "#48C2FA");
                SetBrush(res, "AccentTextBrush", "#0A0A0A");
                SetBrush(res, "DangerBrush", "#FF99A4");
                SetBrush(res, "DangerHoverBgBrush", "#33FF99A4");
                SetBrush(res, "MenuBgBrush", "#2C2C2C");
                SetBrush(res, "MenuBorderBrush", "#454545");
                SetBrush(res, "MenuHoverBrush", "#1AFFFFFF");
                SetBrush(res, "SeparatorBrush", "#1FFFFFFF");
                SetBrush(res, "RevealPillBrush", "#B3FFFFFF");
                SetBrush(res, "DropHighlightBgBrush", "#2660CDFF");
                SetColor(res, "ShadowColor", "#000000");
            }
            else
            {
                // Windows 11 Fluent Light Palette
                SetBrush(res, "WindowBgBrush", "#F3F3F3");
                SetBrush(res, "SurfaceBgBrush", "#F0F9F9F9");
                SetBrush(res, "CardBgBrush", "#FFFFFF");
                SetBrush(res, "CardElevatedBgBrush", "#FCFCFC");
                SetBrush(res, "SurfaceBorderBrush", "#1F000000");
                SetBrush(res, "ControlBorderBrush", "#19000000");
                SetBrush(res, "ControlBgBrush", "#B3FFFFFF");
                SetBrush(res, "ControlHoverBrush", "#0C000000");
                SetBrush(res, "ControlPressedBrush", "#16000000");
                SetBrush(res, "InputBgBrush", "#FFFFFF");
                SetBrush(res, "InputHoverBgBrush", "#FBFBFB");
                SetBrush(res, "InputFocusBgBrush", "#FFFFFF");
                SetBrush(res, "TextPrimaryBrush", "#1A1A1A");
                SetBrush(res, "TextSecondaryBrush", "#5E5E5E");
                SetBrush(res, "TextDisabledBrush", "#9A9A9A");
                SetBrush(res, "AccentBrush", "#0067C0");
                SetBrush(res, "AccentHoverBrush", "#1975C5");
                SetBrush(res, "AccentPressedBrush", "#005A9E");
                SetBrush(res, "AccentTextBrush", "#FFFFFF");
                SetBrush(res, "DangerBrush", "#C42B1C");
                SetBrush(res, "DangerHoverBgBrush", "#1EC42B1C");
                SetBrush(res, "MenuBgBrush", "#FBFBFB");
                SetBrush(res, "MenuBorderBrush", "#D6D6D6");
                SetBrush(res, "MenuHoverBrush", "#0E000000");
                SetBrush(res, "SeparatorBrush", "#14000000");
                SetBrush(res, "RevealPillBrush", "#80000000");
                SetBrush(res, "DropHighlightBgBrush", "#1A0067C0");
                SetColor(res, "ShadowColor", "#000000");
            }

            // Update DWM chrome on all open WPF windows
            foreach (Window window in app.Windows)
            {
                if (window.WindowStyle != WindowStyle.None)
                {
                    WindowHelper.ApplyWindows11Chrome(window, isDark);
                }
            }

            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        private static void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General ||
                e.Category == UserPreferenceCategory.Color)
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(ApplyTheme);
            }
        }

        private static string NormalizeMode(string? mode)
        {
            if (string.Equals(mode, "Dark", StringComparison.OrdinalIgnoreCase)) return "Dark";
            if (string.Equals(mode, "Light", StringComparison.OrdinalIgnoreCase)) return "Light";
            return "System";
        }

        private static void SetBrush(ResourceDictionary res, string key, string hex)
        {
            var brush = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString(hex));
            brush.Freeze();
            res[key] = brush;
        }

        private static void SetColor(ResourceDictionary res, string key, string hex)
        {
            res[key] = (MediaColor)MediaColorConverter.ConvertFromString(hex);
        }
    }
}

