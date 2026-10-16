using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;
using Shawn.Utils.WpfResources.Theme.Styles;

namespace _1RM.View.Editor
{
    /// <summary>
    /// PasswordPopupDialogView.xaml 的交互逻辑
    /// </summary>
    public partial class PasswordPopupDialogView : _1RM.View.Utils.DialogWindowBase
    {
        public PasswordPopupDialogView()
        {
            InitializeComponent();
        }

        protected override void OnDialogClose()
        {
            if (DataContext is PasswordPopupDialogViewModel { DialogResult: null } model) model.CmdQuit.Execute(null);
            else base.OnDialogClose();
        }

        private void TbUserName_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (CbUsePrivateKeyForConnect.IsChecked == true)
                {
                    TbPrivateKey.Focus();
                }
                else
                {
                    TbPwd.Focus();
                }
            }
        }

        private void TbPwd_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnSave.Command.Execute(null);
            }
        }

        private void ButtonOpenPrivateKey_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is PasswordPopupDialogViewModel vm)
            {
                var path = SelectFileHelper.OpenFile(filter: "ppk|*.*", currentDirectoryForShowingRelativePath: Environment.CurrentDirectory, selectedFileName: vm.PrivateKey);
                if (path == null) return;
                vm.PrivateKey = path;
            }
        }
    }
}
