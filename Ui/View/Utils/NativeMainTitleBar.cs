using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace _1RM.View.Utils;

// The same Windows App SDK Tall caption used by LightNote. Windows owns all
// three caption buttons, Snap Layouts, caption dragging and the system menu.
internal sealed class NativeMainTitleBar
{
    private readonly Window _window;
    private readonly RowDefinition? _row;
    private readonly FrameworkElement _title;
    private readonly FrameworkElement _menu;
    private readonly Action _close;
    private readonly FrameworkElement? _search;
    private readonly Func<AppWindowTitleBar, double, double, RectInt32[]>? _captionLayout;
    private RectInt32[]? _lastDragRectangles;
    private readonly DependencyPropertyDescriptor _backgroundDescriptor;
    private AppWindow? _appWindow;
    private bool _forwardingClose;

    internal NativeMainTitleBar(Window window, RowDefinition? row, FrameworkElement title,
        FrameworkElement menu, Action close, FrameworkElement? search = null)
    {
        _window = window;
        _row = row;
        _title = title;
        _menu = menu;
        _close = close;
        _search = search;
        _backgroundDescriptor = DependencyPropertyDescriptor.FromProperty(Control.BackgroundProperty, typeof(Window));
        window.SourceInitialized += Initialize;
    }

    internal NativeMainTitleBar(Window window, FrameworkElement captionContent, Action close,
        Func<AppWindowTitleBar, double, double, RectInt32[]> captionLayout)
        : this(window, null, captionContent, captionContent, close)
    {
        _captionLayout = captionLayout;
    }

    internal bool UsesTallNativeTitleBar { get; private set; }
    internal string? InitializationError { get; private set; }

    private void Initialize(object? sender, EventArgs e)
    {
        try
        {
            if (!AppWindowTitleBar.IsCustomizationSupported())
                throw new NotSupportedException("Windows does not support title bar customization.");
            var handle = new WindowInteropHelper(_window).Handle;
            _appWindow = AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle));
            _appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            _appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            UsesTallNativeTitleBar = true;
            _appWindow.Changed += OnAppWindowChanged;
            _appWindow.Closing += OnClosing;
            _window.SizeChanged += OnSizeChanged;
            _title.SizeChanged += OnSizeChanged;
            if (_search != null)
            {
                _search.SizeChanged += OnSizeChanged;
                _search.IsVisibleChanged += OnSearchVisibilityChanged;
            }
            _window.DpiChanged += OnDpiChanged;
            _window.Activated += UpdateColors;
            _window.Deactivated += UpdateColors;
            _window.Closed += OnClosed;
            if (_captionLayout != null) _window.LayoutUpdated += OnCaptionLayoutUpdated;
            _backgroundDescriptor.AddValueChanged(_window, UpdateColors);
            UpdateMetrics();
            UpdateColors(null, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            InitializationError = exception.ToString();
            UsesTallNativeTitleBar = false;
            _appWindow?.TitleBar.ResetToDefault();
            _menu.Margin = new Thickness(0);
            System.Diagnostics.Trace.TraceError($"Native Tall caption initialization failed: {exception}");
        }
    }

    private void UpdateMetrics()
    {
        if (!UsesTallNativeTitleBar || _appWindow == null) return;
        var dpi = VisualTreeHelper.GetDpi(_window);
        var bar = _appWindow.TitleBar;
        double height = bar.Height / dpi.DpiScaleY;
        if (height <= 0) height = 48;
        double left = Math.Max(0, bar.LeftInset / dpi.DpiScaleX);
        double right = Math.Max(0, bar.RightInset / dpi.DpiScaleX);
        double buttonWidth = right > 0 ? right / (_window.ResizeMode == ResizeMode.NoResize ? 1 : 3) : 48;
        SetMetric("NativeCaptionButtonWidth", buttonWidth);
        SetMetric("NativeCaptionButtonHeight", height);
        if (_captionLayout != null)
        {
            var rectangles = _captionLayout(bar, dpi.DpiScaleX, dpi.DpiScaleY);
            if (_lastDragRectangles == null || !_lastDragRectangles.SequenceEqual(rectangles))
            {
                bar.SetDragRectangles(rectangles);
                _lastDragRectangles = rectangles;
            }
            return;
        }
        _row!.Height = new GridLength(height);
        _menu.Margin = new Thickness(0, 0, right, 0);
        _menu.Height = height;
        _title.Margin = new Thickness(left, 0, right + buttonWidth, 0);
        // Exclude the hamburger and native buttons from the caption drag region.
        int dragWidth = Math.Max(0, (int)Math.Floor(_title.ActualWidth * dpi.DpiScaleX));
        int start = Math.Max(0, bar.LeftInset);
        int end = start + dragWidth;
        int captionHeight = (int)Math.Ceiling(height * dpi.DpiScaleY);
        var regions = new List<RectInt32>();
        if (_search is { IsVisible: true, ActualWidth: > 0 } && Grid.GetRow(_search) == 0)
        {
            var origin = _search.TranslatePoint(new Point(0, 0), _window);
            int cutStart = Math.Clamp((int)Math.Floor((origin.X - 4) * dpi.DpiScaleX), start, end);
            int cutEnd = Math.Clamp((int)Math.Ceiling((origin.X + _search.ActualWidth + 4) * dpi.DpiScaleX), cutStart, end);
            if (cutStart > start) regions.Add(new RectInt32(start, 0, cutStart - start, captionHeight));
            if (cutEnd < end) regions.Add(new RectInt32(cutEnd, 0, end - cutEnd, captionHeight));
        }
        else regions.Add(new RectInt32(start, 0, dragWidth, captionHeight));
        bar.SetDragRectangles(regions.ToArray());
    }

    private void UpdateColors(object? sender, EventArgs e)
    {
        if (!UsesTallNativeTitleBar || _appWindow == null) return;
        var background = (_window.TryFindResource("PrimaryMidBrush") as SolidColorBrush)?.Color
            ?? Color.FromRgb(50, 50, 51);
        var foreground = (_window.TryFindResource("PrimaryTextBrush") as SolidColorBrush)?.Color
            ?? Color.FromRgb(204, 204, 204);
        bool dark = background.R * .299 + background.G * .587 + background.B * .114 < 128;
        var inactive = dark ? Color.FromRgb(145, 145, 150) : Colors.Gray;
        var hover = dark ? Color.FromRgb(65, 65, 68) : Color.FromRgb(223, 227, 232);
        var pressed = dark ? Color.FromRgb(78, 78, 82) : Color.FromRgb(200, 205, 211);
        SetBrush("NativeCaptionForegroundBrush", _window.IsActive ? foreground : inactive);
        SetBrush("NativeCaptionActiveForegroundBrush", foreground);
        SetBrush("NativeCaptionInactiveForegroundBrush", inactive);
        SetBrush("NativeCaptionHoverBrush", hover);
        SetBrush("NativeCaptionPressedBrush", pressed);
        var bar = _appWindow.TitleBar;
        var transparent = new Windows.UI.Color { A = 0 };
        bar.ButtonBackgroundColor = transparent;
        bar.ButtonInactiveBackgroundColor = transparent;
        bar.ButtonForegroundColor = NativeColor(foreground);
        bar.ButtonInactiveForegroundColor = NativeColor(inactive);
        bar.ButtonHoverForegroundColor = NativeColor(foreground);
        bar.ButtonPressedForegroundColor = NativeColor(foreground);
        bar.ButtonHoverBackgroundColor = NativeColor(hover);
        bar.ButtonPressedBackgroundColor = NativeColor(pressed);
        // Windows preserves the red hover/pressed feedback on its Close button.
        int darkMode = dark ? 1 : 0;
        DwmSetWindowAttribute(new WindowInteropHelper(_window).Handle, 20, ref darkMode, sizeof(int));
    }

    private static Windows.UI.Color NativeColor(Color color) => new()
    { A = color.A, R = color.R, G = color.G, B = color.B };

    private void SetMetric(string key, double value)
    {
        if (_window.Resources[key] is not double current || Math.Abs(current - value) > .01)
            _window.Resources[key] = value;
    }

    private void SetBrush(string key, Color color)
    {
        if (_window.Resources[key] is not SolidColorBrush brush || brush.Color != color)
            _window.Resources[key] = new SolidColorBrush(color);
    }

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_forwardingClose || !_window.ShowInTaskbar) return;
        args.Cancel = true;
        _window.Dispatcher.BeginInvoke(new Action(() =>
        {
            _forwardingClose = true;
            try { _close(); }
            finally { _forwardingClose = false; }
        }));
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidSizeChange && !_window.Dispatcher.HasShutdownStarted)
            _window.Dispatcher.BeginInvoke(new Action(UpdateMetrics));
    }
    private void OnSizeChanged(object sender, SizeChangedEventArgs args) => UpdateMetrics();
    private void OnSearchVisibilityChanged(object sender, DependencyPropertyChangedEventArgs args) => UpdateMetrics();
    private void OnDpiChanged(object sender, DpiChangedEventArgs args) => UpdateMetrics();
    private void OnCaptionLayoutUpdated(object? sender, EventArgs args) => UpdateMetrics();

    private void OnClosed(object? sender, EventArgs args)
    {
        _window.SourceInitialized -= Initialize;
        _window.SizeChanged -= OnSizeChanged;
        _title.SizeChanged -= OnSizeChanged;
        if (_search != null)
        {
            _search.SizeChanged -= OnSizeChanged;
            _search.IsVisibleChanged -= OnSearchVisibilityChanged;
        }
        _window.DpiChanged -= OnDpiChanged;
        _window.Activated -= UpdateColors;
        _window.Deactivated -= UpdateColors;
        _window.Closed -= OnClosed;
        _window.LayoutUpdated -= OnCaptionLayoutUpdated;
        _backgroundDescriptor.RemoveValueChanged(_window, UpdateColors);
        if (_appWindow != null)
        {
            _appWindow.Changed -= OnAppWindowChanged;
            _appWindow.Closing -= OnClosing;
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
}
