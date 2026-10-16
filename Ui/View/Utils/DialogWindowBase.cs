using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Shell;
using _1RM.Controls;
using Shawn.Utils.WpfResources.Theme.Styles;

namespace _1RM.View.Utils;

[ContentProperty(nameof(DialogContent))]
public class DialogWindowBase : WindowChromeBase
{
    private readonly ContentPresenter _body;
    private readonly NativeMainTitleBar _nativeTitleBar;
    public object? DialogContent { get => _body.Content; set => _body.Content = value; }

    public DialogWindowBase()
    {
        Width = 560; MinWidth = 360; MaxWidth = 800;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.SingleBorderWindow;
        AllowsTransparency = false;
        SizeToContent = SizeToContent.Height;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowChrome.SetWindowChrome(this, null);
        SetResourceReference(BackgroundProperty, "PrimaryMidBrush");
        SetResourceReference(FontFamilyProperty, "GlobalFontFamily");
        SetResourceReference(FontSizeProperty, "GlobalFontSizeBody");
        SetResourceReference(ForegroundProperty, "BackgroundTextBrush");
        var decorator = new FrameworkElementFactory(typeof(AdornerDecorator));
        decorator.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
        Template = new ControlTemplate(typeof(WindowChromeBase)) { VisualTree = decorator };
        var frame = new Grid(); frame.SetResourceReference(Panel.BackgroundProperty, "PrimaryMidBrush");
        var captionRow = new RowDefinition { Height = new GridLength(48) };
        frame.RowDefinitions.Add(captionRow); frame.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var caption = new Grid(); caption.SetResourceReference(Panel.BackgroundProperty, "PrimaryMidBrush");
        var title = new WindowCaptionTitle(); title.SetBinding(WindowCaptionTitle.TextProperty, new Binding(nameof(Title)) { Source = this });
        caption.Children.Add(title); frame.Children.Add(caption);
        var reserve = new Border { Width = 0, HorizontalAlignment = HorizontalAlignment.Right }; frame.Children.Add(reserve);
        _body = new ContentPresenter();
        var surface = new Border { Child = _body }; surface.SetResourceReference(Border.BackgroundProperty, "BackgroundBrush");
        Grid.SetRow(surface, 1); frame.Children.Add(surface);
        base.Content = frame;
        _nativeTitleBar = new NativeMainTitleBar(this, captionRow, caption, reserve, OnDialogClose, interceptToolWindowClose: true);
    }

    protected virtual void OnDialogClose() => Close();

    protected override void OnSourceInitialized(System.EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (_body.Content is DependencyObject content) KeyboardAction.EnableButtons(content);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Key != Key.Escape) return;
        e.Handled = true; OnDialogClose();
    }
}
