using System.Drawing;
using System.Drawing.Drawing2D;
using WinForms = System.Windows.Forms;

namespace DesktopToolbar.Services
{
    /// <summary>
    /// Windows 11 Fluent Light/Dark renderer for the WinForms system tray ContextMenuStrip.
    /// </summary>
    internal sealed class FluentTrayMenuRenderer : WinForms.ToolStripProfessionalRenderer
    {
        private readonly bool _isDark;

        public FluentTrayMenuRenderer(bool isDark)
            : base(new FluentTrayColorTable(isDark))
        {
            _isDark = isDark;
            RoundedEdges = true;
        }

        protected override void OnRenderItemText(WinForms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = _isDark
                ? Color.FromArgb(243, 243, 243)
                : Color.FromArgb(26, 26, 26);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderMenuItemBackground(WinForms.ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected)
            {
                base.OnRenderMenuItemBackground(e);
                return;
            }

            var rect = new Rectangle(4, 2, e.Item.Width - 8, e.Item.Height - 4);
            using var path = CreateRoundedRect(rect, 4);
            using var brush = new SolidBrush(_isDark
                ? Color.FromArgb(58, 58, 58)
                : Color.FromArgb(232, 232, 232));
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(brush, path);
        }

        protected override void OnRenderSeparator(WinForms.ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using var pen = new Pen(_isDark
                ? Color.FromArgb(62, 62, 62)
                : Color.FromArgb(224, 224, 224));
            e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
        }

        private static GraphicsPath CreateRoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private sealed class FluentTrayColorTable : WinForms.ProfessionalColorTable
        {
            private readonly bool _isDark;

            public FluentTrayColorTable(bool isDark)
            {
                _isDark = isDark;
                UseSystemColors = false;
            }

            public override Color ToolStripDropDownBackground =>
                _isDark ? Color.FromArgb(44, 44, 44) : Color.FromArgb(250, 250, 250);

            public override Color MenuBorder =>
                _isDark ? Color.FromArgb(68, 68, 68) : Color.FromArgb(214, 214, 214);

            public override Color ImageMarginGradientBegin => ToolStripDropDownBackground;
            public override Color ImageMarginGradientMiddle => ToolStripDropDownBackground;
            public override Color ImageMarginGradientEnd => ToolStripDropDownBackground;

            public override Color CheckBackground =>
                _isDark ? Color.FromArgb(0, 120, 212) : Color.FromArgb(215, 235, 252);

            public override Color CheckSelectedBackground => CheckBackground;
        }
    }
}

