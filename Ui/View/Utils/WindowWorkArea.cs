using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace _1RM.View.Utils;

// Keep secondary windows usable on the monitor that contains them, including
// after a move to a smaller monitor or a change in its display scale.
internal sealed class WindowWorkArea
{
    private readonly Window _window;
    private Size _minimum, _maximum;
    private IntPtr _monitor;
    private bool _queued, _closed;

    internal WindowWorkArea(Window window)
    {
        _window = window;
        window.SourceInitialized += OnSourceInitialized;
        window.Loaded += OnLoaded;
        window.DpiChanged += OnDpiChanged;
        window.LocationChanged += OnLocationChanged;
        window.Closed += OnClosed;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _minimum = new Size(_window.MinWidth, _window.MinHeight);
        _maximum = new Size(_window.MaxWidth, _window.MaxHeight);
        Apply(false);
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => QueueApply();
    private void OnDpiChanged(object sender, DpiChangedEventArgs e) => QueueApply();
    private void OnLocationChanged(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(_window).Handle;
        if (handle != IntPtr.Zero && MonitorFromWindow(handle, 2) != _monitor) QueueApply();
    }

    private void QueueApply()
    {
        if (_queued || _closed) return;
        _queued = true;
        _window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            _queued = false;
            if (!_closed) Apply(true);
        }));
    }

    internal static Size FitSize(Size desired, Size available, Size minimum, Size maximum) => new(
        Math.Clamp(desired.Width, Math.Min(minimum.Width, available.Width), Math.Min(maximum.Width, available.Width)),
        Math.Clamp(desired.Height, Math.Min(minimum.Height, available.Height), Math.Min(maximum.Height, available.Height)));

    private void Apply(bool reposition)
    {
        var handle = new WindowInteropHelper(_window).Handle;
        if (handle == IntPtr.Zero) return;
        _monitor = MonitorFromWindow(handle, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(_monitor, ref info)) return;
        var dpi = VisualTreeHelper.GetDpi(_window);
        var available = new Size(Math.Max(1, (info.Work.Right - info.Work.Left) / dpi.DpiScaleX),
            Math.Max(1, (info.Work.Bottom - info.Work.Top) / dpi.DpiScaleY));
        _window.MinWidth = Math.Min(_minimum.Width, available.Width);
        _window.MinHeight = Math.Min(_minimum.Height, available.Height);
        _window.MaxWidth = Math.Min(_maximum.Width, available.Width);
        _window.MaxHeight = Math.Min(_maximum.Height, available.Height);
        if (_window.WindowState != WindowState.Normal) return;
        var size = FitSize(new Size(double.IsNaN(_window.Width) ? _window.ActualWidth : _window.Width,
            double.IsNaN(_window.Height) ? _window.ActualHeight : _window.Height), available, _minimum, _maximum);
        if (!double.IsNaN(_window.Width)) _window.Width = size.Width;
        if (!double.IsNaN(_window.Height)) _window.Height = size.Height;
        // Native coordinates avoid mixing logical coordinate origins on monitors
        // with different DPI. Do not constrain ordinary dragging on one monitor.
        if (!reposition || !GetWindowRect(handle, out var bounds)) return;
        int left = Math.Clamp(bounds.Left, info.Work.Left, Math.Max(info.Work.Left, info.Work.Right - (bounds.Right - bounds.Left)));
        int top = Math.Clamp(bounds.Top, info.Work.Top, Math.Max(info.Work.Top, info.Work.Bottom - (bounds.Bottom - bounds.Top)));
        if (left != bounds.Left || top != bounds.Top) SetWindowPos(handle, IntPtr.Zero, left, top, 0, 0, 0x0015);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _closed = true;
        _window.SourceInitialized -= OnSourceInitialized;
        _window.Loaded -= OnLoaded;
        _window.DpiChanged -= OnDpiChanged;
        _window.LocationChanged -= OnLocationChanged;
        _window.Closed -= OnClosed;
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
}
