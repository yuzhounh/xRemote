using System.Windows;

namespace _1RM.View.Utils;

public partial class MessageBoxView : DialogWindowBase
{
    public MessageBoxView() => InitializeComponent();

    protected override void OnDialogClose()
    {
        var page = DataContext is MessageBoxViewModel model ? model.PageViewModel : DataContext as MessageBoxPageViewModel;
        if (page?.ClickedButton is { } result && result != MessageBoxResult.None) base.OnDialogClose();
        else if (page?.CancelButton is { } cancel) page.ButtonClicked(cancel.Value);
        else base.OnDialogClose();
    }
}
