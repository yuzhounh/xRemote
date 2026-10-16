using System.Windows.Controls;
using System.Windows.Data;

namespace _1RM.View.Settings;

public partial class SettingsPageView : UserControl
{
    public SettingsPageView() => InitializeComponent();

    internal void ReleaseContent()
    {
        BindingOperations.ClearBinding(PageContent, Stylet.Xaml.View.ModelProperty);
        PageContent.Content = null;
    }
}
