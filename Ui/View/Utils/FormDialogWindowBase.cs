using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace _1RM.View.Utils;

// Form cards share the themed native frame and preserve each model's cancel flow.
public class FormDialogWindowBase : DialogWindowBase
{
    private bool _fitting;
    private AppWindow? _nativeWindow;
    private FrameworkElement? _form;
    private ContentPresenter? _actions;
    public object? DialogActions { get; set; }
    public static readonly DependencyProperty CancelCommandProperty = DependencyProperty.Register(
        nameof(CancelCommand), typeof(ICommand), typeof(FormDialogWindowBase));

    public ICommand? CancelCommand
    {
        get => (ICommand?)GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }

    public FormDialogWindowBase()
    {
        SizeToContent = SizeToContent.WidthAndHeight;
        SetBinding(CancelCommandProperty, new Binding("CmdCancel"));
        _ = new WindowWorkArea(this);
    }

    protected override void OnSourceInitialized(System.EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (DialogContent is not FrameworkElement form) return;
        _form = form;
        DialogContent = null;
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.Children.Add(new ScrollViewer
        {
            Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        });
        _actions = new ContentPresenter { Content = DialogActions };
        if (DialogActions is DependencyObject actions) _1RM.Controls.KeyboardAction.EnableButtons(actions, defaultSave: true);
        Grid.SetRow(_actions, 1);
        layout.Children.Add(_actions);
        DialogContent = layout;
    }

    protected override void OnContentRendered(System.EventArgs e)
    {
        base.OnContentRendered(e);
        if (_nativeWindow != null) return;
        _nativeWindow = AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(new WindowInteropHelper(this).Handle));
        // WPF's automatic size includes the old non-client caption after native
        // caption extension. Fit the real client area, including later form changes.
        SizeToContent = SizeToContent.Manual;
        LayoutUpdated += FitClientArea;
        FitClientArea(this, System.EventArgs.Empty);
    }

    private void FitClientArea(object? sender, System.EventArgs e)
    {
        if (_fitting || _nativeWindow == null || _form is not { } body) return;
        _fitting = true;
        try
        {
            // Read the ScrollViewer's completed layout. Measuring its content
            // again here invalidates the viewport and can loop LayoutUpdated.
            var dpi = VisualTreeHelper.GetDpi(this);
            var outer = _nativeWindow.Size; var client = _nativeWindow.ClientSize;
            var width = System.Math.Clamp(System.Math.Max(body.DesiredSize.Width, _actions?.DesiredSize.Width ?? 0)
                + (outer.Width - client.Width) / dpi.DpiScaleX, MinWidth, MaxWidth);
            var contentWidth = System.Math.Max(1, width - (outer.Width - client.Width) / dpi.DpiScaleX);
            var height = System.Math.Clamp(body.DesiredSize.Height + (_actions?.DesiredSize.Height ?? 0)
                + (body.DesiredSize.Width > contentWidth ? SystemParameters.HorizontalScrollBarHeight : 0)
                + _nativeWindow.TitleBar.Height / dpi.DpiScaleY + (outer.Height - client.Height) / dpi.DpiScaleY, MinHeight, MaxHeight);
            var wanted = new SizeInt32((int)System.Math.Ceiling(width * dpi.DpiScaleX), (int)System.Math.Ceiling(height * dpi.DpiScaleY));
            if (outer.Width != wanted.Width || outer.Height != wanted.Height) _nativeWindow.Resize(wanted);
        }
        finally { _fitting = false; }
    }

    protected override void OnDialogClose()
    {
        if (CancelCommand is { } command && command.CanExecute(null)) command.Execute(null);
        else base.OnDialogClose();
    }

    protected override void OnClosed(System.EventArgs e)
    {
        LayoutUpdated -= FitClientArea;
        _nativeWindow = null;
        base.OnClosed(e);
    }
}
