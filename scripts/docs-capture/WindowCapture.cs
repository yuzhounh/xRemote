using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

// Export the application's own native window pixels. PrintWindow excludes the
// desktop pointer and Computer Use's separate click-highlight overlay.
internal static class WindowCapture
{
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int L, T, R, B; }
    delegate bool EnumProc(IntPtr hwnd, IntPtr param);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint thread, EnumProc callback, IntPtr param);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd, uint attribute, out Rect value, uint size);
    static readonly string Request = Path.Combine(AppContext.BaseDirectory, "capture-request.txt");

    internal static void Start()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        timer.Tick += (_, _) => {
            if (!File.Exists(Request)) return;
            var name = File.ReadAllText(Request).Trim();
            File.Delete(Request);
            try { Capture(name); }
            catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "capture-export-error.txt"), ex.ToString()); }
        };
        timer.Start();
    }

    static void Capture(string name)
    {
        if (!Regex.IsMatch(name, "^[a-z][a-z0-9-]+$")) throw new ArgumentException("Invalid capture name");
        string type = name switch {
            "launcher" => "LauncherWindowView",
            "about-version" => "AboutPageView",
            "runner-macros" => "ProtocolHelpWindowView",
            "connection-unsaved" => "MessageBoxView",
            _ when name.StartsWith("connection-") => "ServerEditorWindowView",
            _ when name.StartsWith("settings-") || name.StartsWith("data-") || name == "launcher-settings" || name == "runner-winscp" => "SettingsWindowView",
            _ => "MainWindowView"
        };
        var windows = Application.Current.Windows.Cast<Window>().Where(w => w.IsVisible).ToArray();
        var target = windows.LastOrDefault(w => w.GetType().Name == type)
            ?? throw new InvalidOperationException("Visible target not found: " + type);
        var hwnd = new WindowInteropHelper(target).Handle;
        GetWindowRect(hwnd, out var bounds);
        var managed = windows.Select(w => new WindowInteropHelper(w).Handle).ToHashSet();
        var overlays = new List<IntPtr>();
        EnumThreadWindows(GetCurrentThreadId(), (candidate, _) => {
            if (candidate == hwnd || managed.Contains(candidate) || !IsWindowVisible(candidate) || IsIconic(candidate)) return true;
            if (GetWindowRect(candidate, out var rect) && rect.R > bounds.L && rect.L < bounds.R && rect.B > bounds.T && rect.T < bounds.B)
                overlays.Add(candidate);
            return true;
        }, IntPtr.Zero);
        var full = DwmGetWindowAttribute(hwnd, 9, out var frame, 16) == 0 ? frame : bounds;
        bool transparent = target.AllowsTransparency || type == "LauncherWindowView";
        if (!transparent) { full.L += 3; full.R -= 3; full.B -= 3; }
        var targetFrame = full;
        foreach (var overlay in overlays) {
            GetWindowRect(overlay, out var rect);
            full.L = Math.Min(full.L, rect.L); full.T = Math.Min(full.T, rect.T);
            full.R = Math.Max(full.R, rect.R); full.B = Math.Max(full.B, rect.B);
        }
        using var result = new Bitmap(full.R - full.L, full.B - full.T, PixelFormat.Format32bppArgb);
        using (var canvas = Graphics.FromImage(result)) {
            canvas.Clear(transparent ? System.Drawing.Color.Transparent : System.Drawing.Color.White);
            if (transparent) DrawVisual(hwnd, bounds, full, canvas);
            else Draw(hwnd, bounds, targetFrame, full, canvas);
            overlays.Reverse();
            foreach (var overlay in overlays) { GetWindowRect(overlay, out var rect); DrawVisual(overlay, rect, full, canvas); }
        }
        string directory = Path.Combine(AppContext.BaseDirectory, "png");
        Directory.CreateDirectory(directory);
        result.Save(Path.Combine(directory, name + ".png"), ImageFormat.Png);
        File.WriteAllText(Path.Combine(directory, name + ".txt"), $"{result.Width}x{result.Height}; {target.GetType().Name}; {target.Title}");
    }

    static void DrawVisual(IntPtr hwnd, Rect rect, Rect full, Graphics canvas)
    {
        var visual = HwndSource.FromHwnd(hwnd)?.RootVisual as System.Windows.Media.Visual
            ?? throw new InvalidOperationException("WPF visual not found for transparent window");
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(visual);
        var size = (visual as UIElement)?.RenderSize ?? throw new InvalidOperationException("No visual size");
        var rendered = new System.Windows.Media.Imaging.RenderTargetBitmap(
            (int)Math.Ceiling(size.Width * dpi.DpiScaleX), (int)Math.Ceiling(size.Height * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, System.Windows.Media.PixelFormats.Pbgra32);
        rendered.Render(visual);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rendered));
        using var stream = new MemoryStream();
        encoder.Save(stream); stream.Position = 0;
        using var image = System.Drawing.Image.FromStream(stream);
        canvas.DrawImageUnscaled(image, rect.L - full.L, rect.T - full.T);
    }

    static void Draw(IntPtr hwnd, Rect rect, Rect clip, Rect full, Graphics canvas)
    {
        if (rect.R <= rect.L || rect.B <= rect.T) return;
        using var image = new Bitmap(rect.R - rect.L, rect.B - rect.T, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(image)) {
            var hdc = graphics.GetHdc();
            try { if (!PrintWindow(hwnd, hdc, 2)) throw new InvalidOperationException("Native window export failed"); }
            finally { graphics.ReleaseHdc(hdc); }
        }
        canvas.DrawImage(image, new System.Drawing.Rectangle(clip.L - full.L, clip.T - full.T, clip.R - clip.L, clip.B - clip.T),
            clip.L - rect.L, clip.T - rect.T, clip.R - clip.L, clip.B - clip.T, GraphicsUnit.Pixel);
    }
}
