namespace _1RM.View.Utils;

public partial class BreakingChangeUpdateView : DialogWindowBase
{
    public BreakingChangeUpdateView() => InitializeComponent();
    protected override void OnDialogClose()
    {
        if (DataContext is BreakingChangeUpdateViewModel model) model.CmdClose.Execute(null);
        else base.OnDialogClose();
    }
}
