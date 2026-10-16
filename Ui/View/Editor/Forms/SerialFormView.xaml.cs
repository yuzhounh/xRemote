using System.IO;
using System.Windows;
using System.Windows.Controls;
using _1RM.Utils;
using _1RM.Utils.PuTTY;
using _1RM.Utils.PuTTY.Model;
using _1RM.Utils.PuTTY.Model;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.View.Editor.Forms
{
    public partial class SerialFormView : UserControl
    {
        public SerialFormView()
        {
            InitializeComponent();
        }

        private void ButtonSelectSessionConfigFile_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is SerialFormViewModel vm)
            {
                var path = SelectFileHelper.OpenFile(filter: _1RM.Service.UiText.Get("Editor.SessionFileFilter"));
                if (path == null) return;
                if (File.Exists(path) && KittyConfig.Read(path)?.Count > 0)
                {
                    vm.New.ExternalKittySessionConfigPath = path;
                }
                else
                {
                    vm.New.ExternalKittySessionConfigPath = "";
                    MessageBoxHelper.Warning(_1RM.Service.UiText.Get("Validation.SessionFileInvalid"));
                }
            }
        }
    }
}
