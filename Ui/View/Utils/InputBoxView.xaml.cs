using System.Windows;

namespace _1RM.View.Utils;

public partial class InputBoxView : DialogWindowBase
{
    public InputBoxView()
    {
        InitializeComponent();
        Width = 420;
    }
    protected override void OnDialogClose()
    {
        if (DataContext is InputBoxViewModel { DialogResult: null } model) model.ButtonClicked(MessageBoxResult.Cancel);
        else base.OnDialogClose();
    }
}
