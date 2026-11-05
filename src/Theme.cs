using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Volt
{
    /// <summary>
    /// 设计基础：颜色、字体、圆角、绘制工具。
    /// 字体统一走 GDI（TextRenderer），笔画才均匀；
    /// GDI+ 的 DrawString 不做字形微调，小字会发虚、笔画粗细不匀。
    /// </summary>
    public static class Theme
    {
        // ---- 背景 ----
        public static readonly Color WindowBg = Color.FromArgb(32, 32, 32);
        public static readonly Color WindowBorder = Color.FromArgb(38, 255, 255, 255);

        // ---- 文字（三级就够） ----
        public static readonly Color TextPrimary = Color.FromArgb(248, 248, 248);
        public static readonly Color TextSecondary = Color.FromArgb(150, 255, 255, 255);
        public static readonly Color TextTertiary = Color.FromArgb(96, 255, 255, 255);

        // ---- 语义色 ----
        public static readonly Color Good = Color.FromArgb(74, 222, 128);
        public static readonly Color Warn = Color.FromArgb(251, 191, 36);
        public static readonly Color Bad = Color.FromArgb(248, 113, 113);

        public static readonly Color Divider = Color.FromArgb(20, 255, 255, 255);
        public static readonly Color Track = Color.FromArgb(38, 255, 255, 255);

        // ---- 字体 ----
        // Segoe UI 是 Windows 上字形微调最完整的一套；中文由系统自动链接到微软雅黑。
        public static readonly string Family = PickFamily("Segoe UI", "Microsoft YaHei UI");
        public static readonly string IconFamily = PickFamily("Segoe Fluent Icons", "Segoe MDL2 Assets", "Segoe UI Symbol");

        private static string PickFamily(params string[] names)
        {
            try
            {
                FontFamily[] fams = FontFamily.Families;
                foreach (string want in names)
                {
                    foreach (FontFamily f in fams)
                    {
                        if (string.Equals(f.Name, want, StringComparison.OrdinalIgnoreCase))
                            return want;
                    }
                }
            }
            catch { }
            return names[names.Length - 1];
        }

        public static Font Ui(float size)
        {
            return new Font(Family, size, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static Font UiBold(float size)
        {
            return new Font(Family, size, FontStyle.Bold, GraphicsUnit.Point);
        }

        /// <summary>半粗：Segoe UI 有真正的 Semibold 字重，比 Bold 克制，比 Regular 实在。</summary>
        public static Font UiSemibold(float size)
        {
            try
            {
                return new Font("Segoe UI Semibold", size, FontStyle.Regular, GraphicsUnit.Point);
            }
            catch
            {
                return new Font(Family, size, FontStyle.Bold, GraphicsUnit.Point);
            }
        }

        public static Font Icon(float size)
        {
            return new Font(IconFamily, size, FontStyle.Regular, GraphicsUnit.Point);
        }

        // ---- 文字标志位 ----
        public const TextFormatFlags LeftMid =
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding
            | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;

        public const TextFormatFlags RightMid =
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding
            | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;

        public const TextFormatFlags LeftTop =
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding
            | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;

        public const TextFormatFlags Centered =
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding
            | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;

        // ---- 圆角路径 ----
        public static GraphicsPath Round(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = radius * 2f;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            if (d <= 0.1f)
            {
                p.AddRectangle(r);
                return p;
            }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>图形（圆角、进度条）用的设置。文字不走 GDI+。</summary>
        public static void SetupGraphics(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        }

        public static Color LevelColor(int pct)
        {
            if (pct < 0) return Color.FromArgb(48, 255, 255, 255);
            if (pct <= 15) return Bad;
            if (pct <= 35) return Warn;
            return Good;
        }
    }
}
