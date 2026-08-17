using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DSHDesktop;

// ── 配色（与 DSH-Console 一致的深色纯色系） ─────────────────
internal static class C
{
    public static readonly Color Win = Color.FromArgb(22, 22, 26);
    public static readonly Color Card = Color.FromArgb(30, 30, 36);
    public static readonly Color CardLine = Color.FromArgb(48, 48, 56);
    public static readonly Color Text = Color.FromArgb(235, 235, 240);
    public static readonly Color Dim = Color.FromArgb(150, 150, 160);
    public static readonly Color Accent = Color.FromArgb(77, 107, 254);
    public static readonly Color AccentH = Color.FromArgb(104, 131, 255);
    public static readonly Color Green = Color.FromArgb(74, 222, 128);
    public static readonly Color Amber = Color.FromArgb(250, 204, 21);
    public static readonly Color Red = Color.FromArgb(248, 113, 113);
    public static readonly Color HoverBg = Color.FromArgb(48, 48, 56);
    public static readonly Color DownBg = Color.FromArgb(40, 40, 46);
    public static readonly Color Disabled = Color.FromArgb(42, 42, 48);
}

// ── 圆角路径 ────────────────────────────────────────────────
internal static class G
{
    public static GraphicsPath Rounded(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

// ── 胶囊按钮 ────────────────────────────────────────────────
public enum Glyph { None, Play, Refresh, Folder }

public class PillButton : Button
{
    private bool _hover, _down;
    public Color Fill, FillHover, FillDown;
    public Glyph GlyphType = Glyph.None;

    public PillButton(Color fill, Color hover, Color down)
    {
        Fill = fill; FillHover = hover; FillDown = down;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        BackColor = C.Win;
        Font = new Font("Segoe UI Semibold", 9.5f);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(C.Win);
        Color fill = Enabled ? (_down ? FillDown : (_hover ? FillHover : Fill)) : C.Disabled;
        using (var path = G.Rounded(ClientRectangle, Height / 2))
        using (var brush = new SolidBrush(fill))
        {
            e.Graphics.FillPath(brush, path);
        }

        Color tc = Enabled ? C.Text : Color.FromArgb(110, 110, 120);
        Rectangle textRect;
        if (GlyphType != Glyph.None)
        {
            int cx = 20, cy = Height / 2;
            using var pen = new Pen(tc, 1.6f);
            switch (GlyphType)
            {
                case Glyph.Play:
                    e.Graphics.FillPolygon(pen.Brush, new[]
                    {
                        new Point(cx - 4, cy - 7), new Point(cx + 6, cy), new Point(cx - 4, cy + 7)
                    });
                    break;
                case Glyph.Refresh:
                {
                    float r = 7f, start = 315f, sweep = 270f;
                    e.Graphics.DrawArc(pen, cx - r, cy - r, r * 2, r * 2, start, sweep);
                    float end = (start + sweep) * MathF.PI / 180f;
                    float ex = cx + r * MathF.Cos(end), ey = cy + r * MathF.Sin(end);
                    float tx = -MathF.Sin(end), ty = MathF.Cos(end);
                    float len = 5f;
                    var tip = new PointF(ex + tx * len, ey + ty * len);
                    var b1 = new PointF(ex - tx * len * 0.4f + ty * len * 0.5f, ey - ty * len * 0.4f - tx * len * 0.5f);
                    var b2 = new PointF(ex - tx * len * 0.4f - ty * len * 0.5f, ey - ty * len * 0.4f + tx * len * 0.5f);
                    e.Graphics.DrawLines(pen, new[] { tip, new PointF(ex, ey), b1 });
                    e.Graphics.DrawLine(pen, new PointF(ex, ey), b2);
                    break;
                }
                case Glyph.Folder:
                    e.Graphics.DrawRectangle(pen, cx - 7, cy - 5, 14, 10);
                    e.Graphics.DrawLine(pen, cx - 7, cy - 2, cx - 2, cy - 2);
                    break;
            }
            textRect = new Rectangle(16, 0, Width - 16, Height);
        }
        else
        {
            textRect = ClientRectangle;
        }
        TextRenderer.DrawText(e.Graphics, Text, Font, textRect, tc,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

// ── 圆角卡片 ────────────────────────────────────────────────
public class Card : Control
{
    public Card() { BackColor = C.Win; }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = G.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 12))
        using (var brush = new SolidBrush(C.Card))
        using (var pen = new Pen(C.CardLine))
        {
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
        }
    }
}

// ── 自绘标题按钮 ────────────────────────────────────────────
public class CaptionButton : Control
{
    public bool IsClose { get; set; }
    private bool _hover, _down;

    public CaptionButton()
    {
        Cursor = Cursors.Hand;
        Size = new Size(46, 32);
        BackColor = C.Win;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color bg = _down ? C.DownBg : (_hover ? (IsClose ? Color.FromArgb(110, 220, 90, 90) : C.HoverBg) : C.Win);
        using (var brush = new SolidBrush(bg)) e.Graphics.FillRectangle(brush, ClientRectangle);
        int cx = Width / 2, cy = Height / 2, hw = 4;
        Color line = _hover ? Color.White : C.Dim;
        using var pen = new Pen(line, 1.2f);
        if (IsClose)
        {
            e.Graphics.DrawLine(pen, cx - hw, cy - hw, cx + hw, cy + hw);
            e.Graphics.DrawLine(pen, cx - hw, cy + hw, cx + hw, cy - hw);
        }
        else
        {
            e.Graphics.DrawLine(pen, cx - hw, cy + 2, cx + hw, cy + 2);
            e.Graphics.DrawLine(pen, cx - hw, cy + 4, cx + hw, cy + 4);
        }
    }
}

// ── 主窗口 ──────────────────────────────────────────────────
public class MainForm : Form
{
    private const int TITLE_H = 44;
    private const string WindowTitle = "DeepSeek Harness";

    private WebView2 webView = null!;
    private Panel overlay = null!;               // 覆盖层：Loading / Error
    private Card overlayCard = null!;
    private Label lblStatus = null!, lblDetail = null!;
    private PillButton btnRetry = null!, btnLogs = null!, btnQuit = null!;
    private bool _webReady;
    private bool _starting = true;
    private bool _reallyExit;            // 托盘菜单触发真正退出
    private bool _trayHintShown;
    private Icon? _appIcon;
    private NotifyIcon _tray = null!;
    private DshService _dsh = new();

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private const int WM_NCHITTEST = 0x84;
    private const int HTCAPTION = 2;

    public MainForm()
    {
        Text = WindowTitle;
        ClientSize = new Size(1200, 780);
        MinimumSize = new Size(800, 560);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = C.Win;
        ForeColor = C.Text;
        Font = new Font("Segoe UI", 9.5f);

        try { if (File.Exists("whale.ico")) _appIcon = new Icon("whale.ico"); } catch { }
        if (_appIcon != null) Icon = _appIcon;

        Load += (s, e) =>
        {
            int cp = 2; // Win11 圆角窗口
            DwmSetWindowAttribute(Handle, 33, ref cp, 4);
        };

        BuildLayout();
        BuildTray();
        Shown += async (s, e) => await StartFlowAsync();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST)
        {
            int lp = m.LParam.ToInt32();
            short x = (short)(lp & 0xFFFF), y = (short)(lp >> 16);
            var pt = PointToClient(new Point(x, y));
            if (pt.Y < TITLE_H)
            {
                m.Result = (IntPtr)HTCAPTION;
                return;
            }
        }
        base.WndProc(ref m);
    }

    // ── 布局 ─────────────────────────────────────────────────
    private void BuildLayout()
    {
        // 标题栏
        var pic = new PictureBox
        {
            Location = new Point(18, 9),
            Size = new Size(26, 26),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = C.Win
        };
        try { if (File.Exists("whale-white.png")) pic.Image = Image.FromFile("whale-white.png"); } catch { }
        var title = new Label
        {
            Text = WindowTitle,
            Location = new Point(52, 12),
            AutoSize = true,
            ForeColor = C.Text,
            BackColor = C.Win,
            Font = new Font("Segoe UI Semibold", 10.5f)
        };
        var btnMin = new CaptionButton { Location = new Point(ClientSize.Width - 96, 8) };
        var btnClose = new CaptionButton { IsClose = true, Location = new Point(ClientSize.Width - 46, 8) };
        btnMin.Click += (s, e) => WindowState = FormWindowState.Minimized;
        btnClose.Click += (s, e) => HideToTray();
        Controls.AddRange(new Control[] { pic, title, btnMin, btnClose });

        // WebView2 全区域
        webView = new WebView2
        {
            Location = new Point(0, TITLE_H),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = C.Win
        };
        webView.Size = new Size(ClientSize.Width, ClientSize.Height - TITLE_H);
        webView.CoreWebView2InitializationCompleted += OnWebInit;
        webView.NavigationCompleted += OnNavigationCompleted;
        Controls.Add(webView);
        webView.BringToFront();

        // 覆盖层（Loading / Error）：只覆盖标题栏以下的客户区，不挡标题按钮
        overlay = new Panel
        {
            Location = new Point(0, TITLE_H),
            Size = new Size(ClientSize.Width, ClientSize.Height - TITLE_H),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = C.Win
        };
        overlayCard = new Card { Size = new Size(430, 200), BackColor = C.Win };
        overlayCard.Location = new Point((overlay.Width - overlayCard.Width) / 2, (overlay.Height - overlayCard.Height) / 2);
        overlay.Resize += (s, e) => CenterCard();

        lblStatus = new Label
        {
            Text = "正在启动 DeepSeek Harness…",
            Location = new Point(20, 30),
            AutoSize = true,
            MaximumSize = new Size(390, 60),
            ForeColor = C.Text,
            BackColor = C.Card,
            Font = new Font("Segoe UI Semibold", 12f)
        };
        lblDetail = new Label
        {
            Text = "",
            Location = new Point(20, 80),
            AutoSize = true,
            MaximumSize = new Size(390, 80),
            ForeColor = C.Dim,
            BackColor = C.Card,
            Font = new Font("Segoe UI", 9.5f)
        };
        btnRetry = new PillButton(C.Accent, C.AccentH, C.AccentH) { Text = "重试", GlyphType = Glyph.Refresh, Location = new Point(20, 175), Size = new Size(120, 36), Visible = false };
        btnLogs = new PillButton(C.HoverBg, Color.FromArgb(60, 60, 70), C.DownBg) { Text = "查看日志", GlyphType = Glyph.Folder, Location = new Point(150, 175), Size = new Size(120, 36), Visible = false };
        btnQuit = new PillButton(Color.FromArgb(45, 42, 48), Color.FromArgb(70, 48, 52), Color.FromArgb(56, 40, 44)) { Text = "退出", Location = new Point(280, 175), Size = new Size(110, 36), Visible = false };

        btnRetry.Click += async (s, e) => await StartFlowAsync();
        btnLogs.Click += (s, e) => OpenLogs();
        btnQuit.Click += (s, e) => QuitApp(stopBackend: true);

        overlayCard.Controls.AddRange(new Control[] { lblStatus, lblDetail, btnRetry, btnLogs, btnQuit });
        overlay.Controls.Add(overlayCard);
        Controls.Add(overlay);
        overlay.BringToFront();
    }

    private void CenterCard()
    {
        int cx = Math.Max(0, (overlay.Width - overlayCard.Width) / 2);
        int cy = Math.Max(0, (overlay.Height - overlayCard.Height) / 2);
        overlayCard.Location = new Point(cx, cy);
    }

    private void BuildTray()
    {
        _tray = new NotifyIcon
        {
            Icon = _appIcon ?? SystemIcons.Application,
            Text = "DSH Desktop — DeepSeek Harness",
            Visible = true
        };
        var menu = new ContextMenuStrip();
        menu.Items.Add("显示主窗口", null, (s, e) => ShowMain());
        menu.Items.Add("停止后端并退出", null, (s, e) => QuitApp(stopBackend: true));
        menu.Items.Add("仅退出（保留后端）", null, (s, e) => QuitApp(stopBackend: false));
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (s, e) => ShowMain();
    }

    private void ShowMain()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void HideToTray()
    {
        Hide();
        if (!_trayHintShown)
        {
            _trayHintShown = true;
            _tray.ShowBalloonTip(3000, WindowTitle, "已最小化到托盘，后端服务继续运行。右键托盘图标可退出。", ToolTipIcon.Info);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_reallyExit)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        _tray.Visible = false;
        _tray.Dispose();
        base.OnFormClosing(e);
    }

    private void QuitApp(bool stopBackend)
    {
        if (stopBackend) _dsh.Stop();
        _reallyExit = true;
        Close();
    }

    // ── 启动流程 ─────────────────────────────────────────────
    private async Task StartFlowAsync()
    {
        _starting = true;
        ShowOverlay();
        lblStatus.Text = "正在启动 DeepSeek Harness…";
        lblStatus.ForeColor = C.Text;
        lblDetail.Text = "检查 dsh 环境并等待 WebUI 就绪（首次启动可能需要 10–30 秒）";
        btnRetry.Visible = btnLogs.Visible = btnQuit.Visible = false;

        DshService.StartResult result;
        try { result = await _dsh.StartAsync(); }
        catch (Exception ex) { result = DshService.StartResult.Failed(ex.Message); }

        if (result.Kind == DshService.ResultKind.Ok || result.Kind == DshService.ResultKind.AlreadyRunning)
        {
            // 给后端一点喘息，再加载页面
            await Task.Delay(600);
            lblDetail.Text = "WebUI 已就绪，正在加载界面…";
            await NavigateAsync();
        }
        else
        {
            ShowError(result);
        }
        _starting = false;
    }

    private async Task NavigateAsync()
    {
        // 等待 WebView2 初始化完成（最多 15 秒）
        for (int i = 0; i < 150 && !_webReady; i++)
            await Task.Delay(100);
        if (_webReady)
            webView.CoreWebView2?.Navigate(DshService.Url);
    }

    private void ShowError(DshService.StartResult result)
    {
        ShowOverlay();
        lblStatus.ForeColor = C.Red;
        switch (result.Kind)
        {
            case DshService.ResultKind.NotInstalled:
                lblStatus.Text = "未检测到 dsh";
                lblDetail.Text = "请先安装 DeepSeek Harness：npm install -g @deepseek-ai/dsh，然后重试。";
                break;
            case DshService.ResultKind.PortBusy:
                lblStatus.Text = "端口 3080 已被占用";
                lblDetail.Text = "监听进程：" + (result.Detail ?? "未知") + "\n\n如果该进程不是 DSH，请先释放端口再重试。";
                break;
            default:
                lblStatus.Text = "dsh web 启动失败";
                lblDetail.Text = string.IsNullOrEmpty(result.Detail)
                    ? "启动进程已退出，但未留下错误信息。可查看日志定位问题。"
                    : result.Detail;
                break;
        }
        btnRetry.Visible = true;
        btnLogs.Visible = true;
        btnQuit.Visible = true;
    }

    private void ShowOverlay()
    {
        overlay.Visible = true;
        overlay.BringToFront();
        CenterCard();
    }

    private void OpenLogs()
    {
        try { Process.Start("explorer.exe", DshService.LogDirectory); }
        catch { }
    }

    // ── WebView2 回调 ────────────────────────────────────────
    private void OnWebInit(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            BeginInvoke(() =>
            {
                ShowOverlay();
                lblStatus.ForeColor = C.Red;
                lblStatus.Text = "WebView2 初始化失败";
                lblDetail.Text = e.InitializationException?.Message ?? "未知错误";
                btnRetry.Visible = false;
                btnLogs.Visible = false;
                btnQuit.Visible = true;
            });
            return;
        }

        _webReady = true;
        if (_starting) return; // 启动流程尚未完成，等 NavigateAsync
        BeginInvoke(() => webView.CoreWebView2?.Navigate(DshService.Url));
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess || e.HttpStatusCode != 200 && e.HttpStatusCode != 0)
        {
            BeginInvoke(() =>
            {
                ShowOverlay();
                lblStatus.ForeColor = C.Red;
                lblStatus.Text = "无法加载 WebUI";
                lblDetail.Text = "后端可能已停止或端口被占用。可重试或查看日志。";
                btnRetry.Visible = true;
                btnLogs.Visible = true;
                btnQuit.Visible = true;
            });
            return;
        }

        // 加载成功：隐藏覆盖层，露出 WebUI
        BeginInvoke(() => overlay.Visible = false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dsh.Dispose();
            _tray?.Dispose();
            _appIcon?.Dispose();
        }
        base.Dispose(disposing);
    }
}
