using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace Volt
{
    /// <summary>
    /// 托盘图标：运行时按电量画。
    ///
    /// 这里刻意不用 CreateIconIndirect + Bitmap.GetHbitmap：
    /// GetHbitmap 返回的是 DDB，会丢掉 alpha 通道，
    /// 结果图标在深色任务栏上变成一个看不见的黑块。
    ///
    /// 正确做法是把每个尺寸渲染成 PNG，按 ICO 格式拼进内存流，
    /// 再用 Icon(stream) 加载 —— Windows Vista 以后原生支持 PNG 格式的 ICO 帧，
    /// alpha 完全保留，DPI 缩放也清晰。
    /// </summary>
    public static class TrayIcon
    {
        public static Icon Build()
        {
            return Build(85, false);
        }

        public static Icon Build(int percent, bool charging)
        {
            int[] sizes = new int[] { 16, 20, 24, 32, 48, 256 };

            using (MemoryStream ms = new MemoryStream())
            {
                // leaveOpen: true 必须写 —— BinaryWriter 默认会在 Dispose 时关掉底层流，
                // 那样后面再碰 ms 就会抛 ObjectDisposedException。
                using (BinaryWriter w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true))
                {
                    w.Write((short)0);            // reserved
                    w.Write((short)1);            // type = icon
                    w.Write((short)sizes.Length); // count

                    int headerSize = 6 + 16 * sizes.Length;
                    MemoryStream data = new MemoryStream();

                    foreach (int s in sizes)
                    {
                        byte[] png = RenderPng(s, percent, charging);
                        w.Write((byte)(s >= 256 ? 0 : s));   // width
                        w.Write((byte)(s >= 256 ? 0 : s));   // height
                        w.Write((byte)0);                    // palette
                        w.Write((byte)0);                    // reserved
                        w.Write((short)1);                   // planes
                        w.Write((short)32);                  // bpp
                        w.Write((int)png.Length);
                        w.Write((int)(headerSize + data.Length));
                        data.Write(png, 0, png.Length);
                    }
                    w.Write(data.ToArray());
                }

                ms.Position = 0;
                try
                {
                    // 注意：Icon(Stream) 不会复制数据，必须先把字节取出来，
                    // 用拷贝后的数组重新包一个流，否则 Dispose 外层 MemoryStream 后图标就废了。
                    byte[] bytes = ms.ToArray();
                    using (MemoryStream owned = new MemoryStream(bytes))
                    {
                        return new Icon(owned);
                    }
                }
                catch
                {
                    return SystemIcons.Application;
                }
            }
        }

        private static byte[] RenderPng(int size, int percent, bool charging)
        {
            using (Bitmap bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(Color.Transparent);
                    Draw(g, size, percent, charging);
                }
                using (MemoryStream ms = new MemoryStream())
                {
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    return ms.ToArray();
                }
            }
        }

        private static void Draw(Graphics g, int size, int percent, bool charging)
        {
            float s = size / 16f;

            // 电池壳体：实心浅色块
            float bx = 1.0f * s;
            float by = 4.0f * s;
            float bw = 12.0f * s;
            float bh = 8.0f * s;

            Color shell = Color.FromArgb(255, 236, 240, 245);
            using (GraphicsPath p = Round(new RectangleF(bx, by, bw, bh), 2.2f * s))
            using (SolidBrush b = new SolidBrush(shell))
            {
                g.FillPath(b, p);
            }

            // 正极凸点
            using (GraphicsPath p = Round(
                new RectangleF(bx + bw, by + 2.4f * s, 1.6f * s, 3.2f * s), 0.8f * s))
            using (SolidBrush b = new SolidBrush(shell))
            {
                g.FillPath(b, p);
            }

            // 内槽：深色，让电量柱有对比
            float ix = bx + 1.3f * s;
            float iy = by + 1.3f * s;
            float iw = bw - 2.6f * s;
            float ih = bh - 2.6f * s;

            using (GraphicsPath p = Round(new RectangleF(ix, iy, iw, ih), 1.2f * s))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 32, 34, 38)))
            {
                g.FillPath(b, p);
            }

            // 电量柱
            if (percent > 0)
            {
                float fillW = iw * (percent / 100f);
                if (fillW < 1.4f * s) fillW = 1.4f * s;

                Color fill;
                if (percent <= 15) fill = Color.FromArgb(255, 240, 90, 95);
                else if (percent <= 35) fill = Color.FromArgb(255, 245, 175, 65);
                else fill = Color.FromArgb(255, 90, 215, 145);

                using (GraphicsPath p = Round(new RectangleF(ix, iy, fillW, ih), 1.2f * s))
                using (SolidBrush b = new SolidBrush(fill))
                {
                    g.FillPath(b, p);
                }

                // 充电：柱体上覆一层淡白，表达"正在进电"
                if (charging && percent < 99)
                {
                    using (GraphicsPath p = Round(new RectangleF(ix, iy, fillW, ih), 1.2f * s))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
                    {
                        g.FillPath(b, p);
                    }
                }
            }
        }

        private static GraphicsPath Round(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = radius * 2f;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            if (d <= 0.2f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
