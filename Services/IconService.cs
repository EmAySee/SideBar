using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopToolbar.Services
{
    public static class IconService
    {
        private static readonly Uri AppIconPngUri = new("pack://application:,,,/Assets/app-icon.png", UriKind.Absolute);
        private static readonly Uri AppIconIcoUri = new("pack://application:,,,/Assets/app.ico", UriKind.Absolute);

        public static ImageSource? GetAppIconImage(int decodeWidth = 128)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = AppIconPngUri;
                bmp.DecodePixelWidth = decodeWidth;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        public static Icon CreateTrayIcon()
        {
            try
            {
                var streamInfo = System.Windows.Application.GetResourceStream(AppIconIcoUri);
                if (streamInfo != null)
                {
                    using Stream stream = streamInfo.Stream;
                    return new Icon(stream, new System.Drawing.Size(32, 32));
                }
            }
            catch
            {
                // Fall back to system application icon if resource is missing
            }

            return (Icon)SystemIcons.Application.Clone();
        }
    }
}

