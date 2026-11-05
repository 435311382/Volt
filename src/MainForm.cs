using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Volt
{
    /// <summary>
    /// 托盘图标点开的小浮窗。行为和 Twinkle Tray 一致：
    /// 点托盘图标弹出，点外面自动收起，不进任务栏。
    /// </summary>
    public sealed class MainForm : Form
    {
        // ---------- 布局（逻辑像素，按 DPI 缩放） ----------
        private const int WindowWidth = 344;
        private const int Pad = 16;
        private const int TitleH = 42;
        private const int RowH = 66;
        private const int FooterH = 40;

        private readonly List<DeviceDefinition> _catalog;
        private readonly List<DeviceRow> _rows = new List<DeviceRow>();
        private readonly Timer _timer = new Timer();
        private readonly float _s;

        private NotifyIcon _tray;
        private bool _busy;
        private bool _reallyExit;
        private string _stampText = "正在读取…";
        private DateTime _lastHidden = DateTime.MinValue;

        private Rectangle _refreshRect;
        private Rectangle _closeRect;
        private int _hot = -1;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowW(string cls, string win);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr h, out RECT r);

        public bool ShowOnStart = false;

        public MainForm()
        {
            _s = DeviceDpi / 96f;
            _catalog = Catalog.Build();

            // 刻意不用 OptimizedDoubleBuffer：
            // 它的后台缓冲是 32bppARGB，GDI 文字画进带 alpha 的位图会禁用 ClearType，
            // 结果是笔画发灰、粗细不匀。改成直接绘制 + 抑制背景擦除，文字才是 ClearType。
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw, true);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Text = "Volt";
            BackColor = Theme.WindowBg;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;
            ShowInTaskbar = false;
            TopMost = true;
            MinimizeBox = false;
            MaximizeBox = false;

            BuildLayout();
            BuildTray();

            _timer.Interval = 5000;
            _timer.Tick += delegate { if (Visible) RefreshNow(); };
            _timer.Start();
        }

        /// <summary>
        /// 监听第二实例发来的「显示」信号。
        /// 没有这个，第二次双击快捷方式就会静默退出，用户以为程序坏了。
        /// </summary>
        public void AttachShowEvent(System.Threading.EventWaitHandle ev)
        {
            _showEvent = ev;
            System.Threading.ThreadPool.RegisterWaitForSingleObject(ev,
                delegate
                {
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            PositionFlyout();
                            Show();
                            Activate();
                            RefreshNow();
                        });
                    }
                    catch { }
                }, null, System.Threading.Timeout.Infinite, false);
        }

        private System.Threading.EventWaitHandle _showEvent;

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            PositionFlyout();
            RefreshNow();
        }

        private int Px(double v) { return (int)Math.Round(v * _s); }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000;   // CS_DROPSHADOW
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int round = 2;
                DwmSetWindowAttribute(Handle, 33, ref round, 4);
                int dark = 1;
                DwmSetWindowAttribute(Handle, 20, ref dark, 4);
            }
            catch { }
        }

        private void BuildLayout()
        {
            int rowsH = _catalog.Count * Px(RowH);
            int h = Px(TitleH) + rowsH + Px(FooterH);
            ClientSize = new Size(Px(WindowWidth), h);

            for (int i = 0; i < _catalog.Count; i++)
            {
                DeviceRow row = new DeviceRow(_catalog[i], _s);
                row.Location = new Point(0, Px(TitleH) + i * Px(RowH));
                row.Size = new Size(ClientSize.Width, Px(RowH));
                _rows.Add(row);
                Controls.Add(row);
            }

            _closeRect = new Rectangle(ClientSize.Width - Px(Pad) - Px(14), Px(14), Px(14), Px(14));
            _refreshRect = new Rectangle(ClientSize.Width - Px(Pad) - Px(14),
                ClientSize.Height - Px(FooterH) + Px(13), Px(14), Px(14));
        }

        /// <summary>
        /// 贴在任务栏上方、靠右 —— 就是托盘图标弹出来的那个位置。
        /// 拿不到任务栏就用屏幕工作区右下角。
        /// </summary>
        public void PositionFlyout()
        {
            int gap = Px(10);
            int x, y;

            IntPtr taskbar = FindWindowW("Shell_TrayWnd", null);
            RECT tr;
            if (taskbar != IntPtr.Zero && GetWindowRect(taskbar, out tr))
            {
                x = tr.Right - Width - gap;
                y = tr.Top - Height - gap;
            }
            else
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                x = wa.Right - Width - gap;
                y = wa.Bottom - Height - gap;
            }

            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            if (x < bounds.Left + gap) x = bounds.Left + gap;
            if (y < bounds.Top + gap) y = bounds.Top + gap;

            Location = new Point(x, y);
        }

        // ================================================================
        // 绘制
        // ================================================================

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.SetupGraphics(g);
            g.Clear(Theme.WindowBg);

            using (GraphicsPath p = Theme.Round(
                new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f), Px(8)))
            using (Pen pen = new Pen(Theme.WindowBorder, 1f))
            {
                g.DrawPath(pen, p);
            }

            using (Font f = Theme.UiSemibold(11.5f))
            {
                TextRenderer.DrawText(g, "Volt", f,
                    new Rectangle(Px(Pad), 0, Px(120), Px(TitleH)),
                    Theme.TextPrimary, Theme.LeftMid);
            }

            DrawGlyph(g, "\uE711", _closeRect, _hot == 1 ? Theme.TextPrimary : Theme.TextTertiary, 8f);

            int footerY = ClientSize.Height - Px(FooterH);
            using (Pen p = new Pen(Theme.Divider, 1f))
            {
                g.DrawLine(p, Px(Pad), footerY, ClientSize.Width - Px(Pad), footerY);
            }

            using (Font f = Theme.Ui(9f))
            {
                TextRenderer.DrawText(g, _stampText, f,
                    new Rectangle(Px(Pad), footerY, Px(220), Px(FooterH)),
                    Theme.TextTertiary, Theme.LeftMid);
            }

            DrawGlyph(g, "\uE72C", _refreshRect, _hot == 0 ? Theme.TextPrimary : Theme.TextTertiary, 8.5f);
        }

        private void DrawGlyph(Graphics g, string glyph, Rectangle r, Color color, float size)
        {
            using (Font f = Theme.Icon(size))
            {
                TextRenderer.DrawText(g, glyph, f, r, color, Theme.Centered);
            }
        }

        // ================================================================
        // 交互
        // ================================================================

        // 浮窗刻意不可拖动：它是贴在任务栏上方的弹出面板，
        // 位置由 PositionFlyout() 决定，和 Twinkle Tray 一致。
        // 之前用 HTCAPTION 让标题栏可拖动，结果一点就飘走，很难用。

        private const int WM_ERASEBKGND = 0x0014;

        /// <summary>整块都由 OnPaint 画满，告诉系统别擦背景，否则直接绘制会闪。</summary>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_ERASEBKGND)
            {
                m.Result = (IntPtr)1;
                return;
            }
            base.WndProc(ref m);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hot = -1;
            if (_refreshRect.Contains(e.Location)) hot = 0;
            else if (_closeRect.Contains(e.Location)) hot = 1;
            if (hot != _hot)
            {
                _hot = hot;
                Cursor = hot >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hot != -1) { _hot = -1; Cursor = Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_closeRect.Contains(e.Location)) { HideFlyout(); return; }
            if (_refreshRect.Contains(e.Location)) { RefreshNow(); return; }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { HideFlyout(); return true; }
            if (keyData == Keys.F5) { RefreshNow(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (!_reallyExit && Visible) HideFlyout();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_reallyExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideFlyout();
                return;
            }
            base.OnFormClosing(e);
        }

        public void HideFlyout()
        {
            _lastHidden = DateTime.UtcNow;
            Hide();
        }

        public void ShowFlyout()
        {
            PositionFlyout();
            Show();
            Activate();
            RefreshNow();
        }

        // ================================================================
        // 托盘
        // ================================================================

        private void BuildTray()
        {
            _tray = new NotifyIcon();
            _tray.Icon = TrayIcon.Build(80, false);
            _tray.Text = "Volt";
            _tray.Visible = true;

            _tray.MouseClick += delegate(object s, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;

                if (Visible) { HideFlyout(); return; }
                if ((DateTime.UtcNow - _lastHidden).TotalMilliseconds < 250) return;
                ShowFlyout();
            };

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = Theme.Ui(9f);
            menu.ShowImageMargin = false;
            menu.Items.Add("打开面板", null, delegate { ShowFlyout(); });
            menu.Items.Add("立即刷新", null, delegate { RefreshNow(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { Quit(); });
            _tray.ContextMenuStrip = menu;
        }

        /// <summary>
        /// 托盘图标优先用 exe 里的多尺寸 ICO。
        /// 运行时画的图标在部分 Windows 11 版本上会因 DPI 缩放变得很淡，
        /// 多尺寸资源由系统自己挑，清晰度稳定。
        /// </summary>
        private static Icon LoadAppIcon()
        {
            try
            {
                string exe = System.Windows.Forms.Application.ExecutablePath;
                Icon ic = Icon.ExtractAssociatedIcon(exe);
                if (ic != null) return ic;
            }
            catch { }

            try
            {
                string ico = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath),
                    "app.ico");
                if (System.IO.File.Exists(ico)) return new Icon(ico);
            }
            catch { }

            return TrayIcon.Build(80, false);
        }
        public void Quit()
        {
            _reallyExit = true;
            _timer.Stop();
            if (_tray != null) _tray.Visible = false;
            Close();
        }

        // ================================================================
        // 数据
        // ================================================================

        public void RefreshNow()
        {
            if (_busy) return;
            _busy = true;
            _stampText = "正在读取…";
            Invalidate();

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                List<DeviceState> states = null;
                string err = null;
                try { states = Scanner.Scan(_catalog); }
                catch (Exception ex) { err = ex.Message; }

                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        Apply(states, err);
                        _busy = false;
                    });
                }
                catch { _busy = false; }
            });
        }

        private void Apply(List<DeviceState> states, string err)
        {
            if (states == null)
            {
                _stampText = "读取失败";
                Invalidate();
                return;
            }

            _stampText = "更新于 " + DateTime.Now.ToString("HH:mm:ss");

            List<string> tips = new List<string>();
            int lowest = -1;
            bool charging = false;

            for (int i = 0; i < _rows.Count && i < states.Count; i++)
            {
                _rows[i].State = states[i];
                DeviceState s = states[i];
                if (s.Present && s.Reading != null && s.Reading.Ok)
                {
                    tips.Add(s.Def.DisplayName + " " + s.Percent + "%");
                    if (lowest < 0 || s.Percent < lowest) lowest = s.Percent;
                    if (s.Charge == ChargeState.Charging) charging = true;
                    if (s.Charge == ChargeState.Charging) charging = true;
                }
            }

            if (_tray != null)
            {
                string text = tips.Count > 0 ? string.Join("  ·  ", tips.ToArray()) : "未检测到设备";
                if (text.Length > 63) text = text.Substring(0, 63);
                _tray.Text = text;

                Icon old = _tray.Icon;
                Icon next = TrayIcon.Build(lowest < 0 ? 0 : lowest, charging);
                if (next != null)
                {
                    _tray.Icon = next;
                    if (old != null) old.Dispose();
                }
            }

            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
                _tray = null;
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// 一行设备。不画卡片底、不画描边——
    /// 靠留白和一条细轨道分隔，这是扁平设计的关键。
    /// 结构：图标 + 名称 + 连接/状态，一条全宽细轨道，右侧大号百分比。
    /// </summary>
    public sealed class DeviceRow : Control
    {
        private DeviceState _state;
        private readonly float _s;

        public DeviceRow(DeviceDefinition def, float scale)
        {
            _s = scale;
            // 同上：不用双缓冲，保住 ClearType
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.WindowBg;
            _state = new DeviceState();
            _state.Def = def;
            _state.Error = "正在读取…";
        }

        public DeviceState State
        {
            get { return _state; }
            set { _state = value; Invalidate(); }
        }

        private int Px(double v) { return (int)Math.Round(v * _s); }

        private static string KindGlyph(string kind)
        {
            if (kind == "鼠标") return "\uE962";
            if (kind == "键盘") return "\uE765";
            if (kind == "耳机") return "\uE7F6";
            return "\uE7F8";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.SetupGraphics(g);
            g.Clear(Theme.WindowBg);

            bool live = _state.Present && _state.Reading != null && _state.Reading.Ok;
            int pct = live ? _state.Percent : -1;

            int pad = Px(16);
            int iconW = Px(24);
            int textX = pad + iconW + Px(6);

            // ---- 图标 ----
            using (Font f = Theme.Icon(12f))
            {
                TextRenderer.DrawText(g, KindGlyph(_state.Def.Kind), f,
                    new Rectangle(pad, 0, iconW, Px(32)),
                    live ? Theme.TextPrimary : Theme.TextTertiary, Theme.LeftMid);
            }

            // ---- 名称 ----
            using (Font f = Theme.UiSemibold(11.5f))
            {
                TextRenderer.DrawText(g, _state.Def.DisplayName, f,
                    new Rectangle(textX, Px(5), Width - textX - Px(86), Px(19)),
                    Theme.TextPrimary, Theme.LeftMid);
            }

            // ---- 连接方式 · 状态 ----
            string sub;
            Color subColor = Theme.TextTertiary;
            if (!_state.Present)
            {
                sub = "未连接";
            }
            else if (!live)
            {
                sub = _state.Error.Length > 0 ? _state.Error : "未就绪";
            }
            else
            {
                string transport = _state.Transport.Length > 0 ? _state.Transport : _state.Def.Kind;
                string st;
                if (_state.Charge == ChargeState.Charging) { st = "充电中"; subColor = Theme.Good; }
                else if (_state.Charge == ChargeState.Full) { st = "已充满"; subColor = Theme.Good; }
                else if (_state.Charge == ChargeState.Discharging) st = "使用中";
                else st = "状态未知";

                if (_state.Stale) { st = "上次 " + _state.LastGood.ToString("HH:mm"); subColor = Theme.TextTertiary; }
                sub = transport + "  ·  " + st;
            }

            using (Font f = Theme.Ui(9f))
            {
                TextRenderer.DrawText(g, sub, f,
                    new Rectangle(textX, Px(25), Width - textX - Px(86), Px(15)),
                    subColor, Theme.LeftMid);
            }

            // ---- 百分比：数字大、百分号小，整体右对齐 ----
            string num = live ? pct.ToString() : "—";
            using (Font fNum = Theme.UiSemibold(15f))
            using (Font fPct = Theme.Ui(9f))
            {
                Size nsz = TextRenderer.MeasureText(num, fNum, new Size(200, 100),
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                Size psz = live
                    ? TextRenderer.MeasureText("%", fPct, new Size(100, 100),
                        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine)
                    : new Size(0, 0);

                int total = nsz.Width + psz.Width;
                int sx = Width - pad - total;

                TextRenderer.DrawText(g, num, fNum,
                    new Rectangle(sx, 0, nsz.Width + Px(2), Px(30)),
                    live ? Theme.TextPrimary : Theme.TextTertiary, Theme.LeftMid);

                if (live)
                {
                    TextRenderer.DrawText(g, "%", fPct,
                        new Rectangle(sx + nsz.Width, Px(12), psz.Width + Px(2), Px(14)),
                        Theme.TextSecondary, Theme.LeftMid);
                }
            }

            // ---- 轨道：全宽，细，圆头 ----
            int barX = pad;
            int barW = Width - pad * 2;
            int barH = Px(3);
            int barY = Height - Px(18);

            using (GraphicsPath p = Theme.Round(new RectangleF(barX, barY, barW, barH), barH / 2f))
            using (SolidBrush b = new SolidBrush(Theme.Track))
            {
                g.FillPath(b, p);
            }

            if (live && pct > 0)
            {
                float fillW = barW * (pct / 100f);
                if (fillW < barH) fillW = barH;
                Color c = Theme.LevelColor(pct);
                if (_state.Stale) c = Color.FromArgb(120, c.R, c.G, c.B);
                using (GraphicsPath p = Theme.Round(new RectangleF(barX, barY, fillW, barH), barH / 2f))
                using (SolidBrush b = new SolidBrush(c))
                {
                    g.FillPath(b, p);
                }
            }
        }
    }
}
