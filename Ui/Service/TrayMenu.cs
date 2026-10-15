using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace _1RM.Service;

// Tray-only styling, matching LinkFlow / LightNote / Soft Trace metrics.
internal sealed class TrayMenu : ContextMenuStrip
{
    internal Color TextColor { get; private set; } = Color.FromArgb(0x1F, 0x23, 0x28);
    internal Color HoverColor { get; private set; } = Color.FromArgb(0xF3, 0xF4, 0xF6);
    internal Color BorderColor { get; private set; } = Color.FromArgb(0xDC, 0xE0, 0xE5);
    internal Color SeparatorColor { get; private set; } = Color.FromArgb(0xD9, 0xD9, 0xD9);
    internal Color CheckColor => TextColor;
    internal Color DisabledColor { get; private set; } = Color.FromArgb(104, 113, 125);
    private readonly Func<bool> _isDark;

    private Font _menuFont = new("Microsoft YaHei UI", 14f, GraphicsUnit.Pixel);

    public TrayMenu(Func<bool> isDark)
    {
        _isDark = isDark;
        Font = _menuFont;
        ShowImageMargin = false;
        ShowCheckMargin = false;
        BackColor = Color.White;
        ForeColor = TextColor;
        Renderer = new TrayMenuRenderer(this);
        DropShadowEnabled = !TrayMenuNative.IsWindows11;
    }

    private float _uiScale = 1f;

    internal float UiScale => _uiScale;
    internal Font MenuFont => _menuFont;
    protected override Padding DefaultPadding => new(0, Px(8), 0, Px(8));

    internal bool IsChecked(ToolStripItem item) => item is ToolStripMenuItem { Checked: true };

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _menuFont.Dispose();
    }

    // Let DWM provide the same soft popup shadow as other Windows 11 applications.
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            if (TrayMenuNative.IsWindows11) parameters.Style |= 0x00040000; // WS_THICKFRAME
            return parameters;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (TrayMenuNative.IsWindows11 && m.Msg == 0x0083 && m.WParam != IntPtr.Zero) // WM_NCCALCSIZE
        {
            m.Result = IntPtr.Zero;
            return;
        }
        base.WndProc(ref m);
        if (!TrayMenuNative.IsWindows11) return;
        if (m.Msg == 0x0024) // WM_GETMINMAXINFO: do not impose a normal window's minimum width.
        {
            System.Runtime.InteropServices.Marshal.WriteInt32(m.LParam, 24, 1);
            System.Runtime.InteropServices.Marshal.WriteInt32(m.LParam, 28, 1);
        }
        if (m.Msg == 0x0084 && m.Result.ToInt32() is >= 10 and <= 18) m.Result = new IntPtr(1);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        TrayMenuNative.StyleMenuWindow(Handle, BorderColor, _isDark());
    }

    protected override void OnOpening(CancelEventArgs e)
    {
        base.OnOpening(e); // Dynamic submenus populate before measuring.
        bool dark = _isDark();
        BackColor = dark ? Color.FromArgb(0x25, 0x26, 0x29) : Color.White;
        ForeColor = TextColor = dark ? Color.FromArgb(0xEE, 0xEF, 0xF1) : Color.FromArgb(0x1F, 0x23, 0x28);
        HoverColor = dark ? Color.FromArgb(0x36, 0x38, 0x3D) : Color.FromArgb(0xF3, 0xF4, 0xF6);
        BorderColor = dark ? Color.FromArgb(0x45, 0x48, 0x50) : Color.FromArgb(0xDC, 0xE0, 0xE5);
        SeparatorColor = dark ? Color.FromArgb(0x45, 0x48, 0x50) : Color.FromArgb(0xD9, 0xD9, 0xD9);
        DisabledColor = dark ? Color.FromArgb(0x9B, 0xA1, 0xAA) : Color.FromArgb(104, 113, 125);
        ApplyMetrics(TrayMenuNative.GetDpiAtCursor());
        if (IsHandleCreated) TrayMenuNative.StyleMenuWindow(Handle, BorderColor, dark);
    }

    private int Px(float value) => (int)Math.Round(value * UiScale);

    /// <summary>Sizes everything in physical pixels for the monitor the menu opens on.</summary>
    private void ApplyMetrics(int dpi)
    {
        _uiScale = dpi / 96f;

        var oldFont = _menuFont;
        _menuFont = new Font("Microsoft YaHei UI", 14f * UiScale, GraphicsUnit.Pixel);
        Font = _menuFont;
        oldFont.Dispose();

        AutoSize = false; // ToolStripDropDownMenu otherwise replaces the measured width.
        SuspendLayout();
        Padding = new Padding(0, Px(8), 0, Px(8));

        int textWidth = 0;
        foreach (ToolStripItem item in Items)
        {
            if (item is ToolStripSeparator) continue;
            var size = TextRenderer.MeasureText(item.Text ?? "", _menuFont, Size.Empty,
                TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            textWidth = Math.Max(textWidth, size.Width);
        }

        int width = Math.Max(Px(188), textWidth + Px(64));
        int height = Padding.Vertical;
        foreach (ToolStripItem item in Items)
        {
            item.AutoSize = false;
            bool separator = item is ToolStripSeparator;
            item.Margin = separator ? Padding.Empty : new Padding(Px(4), 0, Px(4), 0);
            item.Padding = Padding.Empty;
            item.Size = new Size(separator ? width : width - Px(8), separator ? Px(17) : Px(32));
            height += item.Height;
        }
        Size = new Size(width, height);
        ResumeLayout(true);
    }
}

internal sealed class TrayMenuRenderer : ToolStripRenderer
{
    private readonly TrayMenu _menu;

    public TrayMenuRenderer(TrayMenu menu) => _menu = menu;

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        e.Graphics.Clear(_menu.BackColor);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        if (TrayMenuNative.IsWindows11) return; // DWM draws the rounded border on Windows 11
        using var pen = new Pen(_menu.BorderColor);
        e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;
        float s = _menu.UiScale;
        var rect = new RectangleF(0, 0, e.Item.Width, e.Item.Height);
        var g = e.Graphics;
        var previous = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = RoundedRect(rect, 6 * s))
        using (var brush = new SolidBrush(_menu.HoverColor))
        {
            g.FillPath(brush, path);
        }
        g.SmoothingMode = previous;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        float s = _menu.UiScale;
        var item = e.Item;
        int left = (int)Math.Round(16 * s);
        var rect = new Rectangle(left, 0, Math.Max(0, item.Width - left - (int)Math.Round(26 * s)), item.Height);
        TextRenderer.DrawText(e.Graphics, e.Text, _menu.MenuFont, rect,
            item.Enabled ? _menu.TextColor : _menu.DisabledColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
            TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

        if (_menu.IsChecked(item)) DrawCheckMark(e.Graphics, item, s);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) { }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        if (e.Item == null) return;
        float s = _menu.UiScale;
        float x = e.Item.Width - 20 * s;
        float y = e.Item.Height / 2f;
        bool left = e.Direction == ArrowDirection.Left;
        float direction = left ? -1 : 1;
        var previous = e.Graphics.SmoothingMode;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(e.Item.Enabled ? _menu.TextColor : _menu.DisabledColor, 1.4f * s)
        { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        e.Graphics.DrawLines(pen, new[] {
            new PointF(x - direction * 2 * s, y - 4 * s),
            new PointF(x + direction * 2 * s, y),
            new PointF(x - direction * 2 * s, y + 4 * s) });
        e.Graphics.SmoothingMode = previous;
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        float s = _menu.UiScale;
        int inset = 0;
        int thickness = Math.Max(1, (int)Math.Round(s));
        int y = (e.Item.Height - thickness) / 2;
        using var brush = new SolidBrush(_menu.SeparatorColor);
        e.Graphics.FillRectangle(brush, inset, y, Math.Max(0, e.Item.Width - inset * 2), thickness);
    }

    private void DrawCheckMark(Graphics g, ToolStripItem item, float s)
    {
        float cx = item.Width - 20 * s;
        float cy = item.Height / 2f;
        var previous = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var pen = new Pen(_menu.CheckColor, Math.Max(1.5f, 1.7f * s)))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            g.DrawLines(pen, new[]
            {
                new PointF(cx - 4 * s, cy),
                new PointF(cx - 1.3f * s, cy + 2.8f * s),
                new PointF(cx + 4 * s, cy - 3.2f * s),
            });
        }
        g.SmoothingMode = previous;
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal static class TrayMenuNative
{
    internal static bool IsWindows11 => Environment.OSVersion.Version.Build >= 22000;

    internal static int GetDpiAtCursor()
    {
        var cursor = Cursor.Position;
        var monitor = MonitorFromPoint(new Point(cursor.X, cursor.Y), 2);
        return GetDpiForMonitor(monitor, 0, out uint dpi, out _) == 0 && dpi > 0 ? (int)dpi : 96;
    }

    internal static void StyleMenuWindow(IntPtr handle, Color color, bool dark)
    {
        if (!IsWindows11) return;
        int round = 2;
        DwmSetWindowAttribute(handle, 33, ref round, sizeof(int));
        int border = color.R | (color.G << 8) | (color.B << 16);
        DwmSetWindowAttribute(handle, 34, ref border, sizeof(int));
        int immersive = dark ? 1 : 0;
        DwmSetWindowAttribute(handle, 20, ref immersive, sizeof(int));
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(Point point, uint flags);
    [System.Runtime.InteropServices.DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
}
