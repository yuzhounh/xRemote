using System.Windows;

namespace _1RM.View.Utils;

public partial class MessageBoxView : DialogWindowBase
{
    public MessageBoxView()
    {
        InitializeComponent();
        ContentRendered += (_, _) =>
        {
            if (DialogContent is FrameworkElement body) CompactDialogSizing.Fit(this, body);
        };
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var page = DataContext is MessageBoxViewModel model ? model.PageViewModel : DataContext as MessageBoxPageViewModel;
        if (e.Handled || e.Key != System.Windows.Input.Key.Enter || page?.ShowButtons != false) return;
        e.Handled = true;
        OnDialogClose();
    }

    protected override void OnDialogClose()
    {
        var page = DataContext is MessageBoxViewModel model ? model.PageViewModel : DataContext as MessageBoxPageViewModel;
        if (page?.ClickedButton is { } result && result != MessageBoxResult.None) base.OnDialogClose();
        else if (page?.CancelButton is { } cancel) page.ButtonClicked(cancel.Value);
        else base.OnDialogClose();
    }
}
