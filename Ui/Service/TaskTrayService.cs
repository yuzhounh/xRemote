using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using _1RM.Model;
using _1RM.Utils;
using _1RM.View;
using _1RM.View.Host;
using Dragablz.Dockablz;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Controls;
using Stylet;
using Application = System.Windows.Application;

namespace _1RM.Service
{
    public class TaskTrayService
    {

        ~TaskTrayService()
        {
            TaskTrayDispose();
        }

        #region TaskTray

        private System.Windows.Forms.NotifyIcon? _taskTrayIcon = null;
        public void TaskTrayInit()
        {
            Debug.Assert(Application.GetResourceStream(ResourceUriHelper.GetUriFromCurrentAssembly("LOGO.ico"))?.Stream != null);
#if DEBUG
            var icon = new System.Drawing.Icon(Application.GetResourceStream(ResourceUriHelper.GetUriFromCurrentAssembly("LOGO_D.ico")).Stream);
#else
            var icon = new System.Drawing.Icon(Application.GetResourceStream(ResourceUriHelper.GetUriFromCurrentAssembly("LOGO.ico")).Stream);
#endif
            Debug.Assert(icon != null);
            Execute.OnUIThreadSync(() =>
            {
                _taskTrayIcon ??= new System.Windows.Forms.NotifyIcon
                {
                    Text = Assert.APP_DISPLAY_NAME,
                    BalloonTipText = "",
                    Icon = icon,
                    Visible = true
                };
                _taskTrayIcon.Visible = false;
                ReloadTaskTrayContextMenu();
                GlobalEventHelper.OnLanguageChanged -= ReloadTaskTrayContextMenu;
                GlobalEventHelper.OnLanguageChanged += ReloadTaskTrayContextMenu;
                _taskTrayIcon.MouseClick -= TaskTrayIconOnMouseDoubleClick;
                _taskTrayIcon.MouseClick += TaskTrayIconOnMouseDoubleClick;
                _taskTrayIcon.Visible = true;
            });
        }

        private void TaskTrayIconOnMouseDoubleClick(object? sender, MouseEventArgs e)
        {
            if (_taskTrayIcon?.Visible == true
                && e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                IoC.Get<MainWindowViewModel>()?.ShowMe();
            }
        }

        public void TaskTrayDispose()
        {
            // Unsubscribe from static events to prevent memory leaks
            GlobalEventHelper.OnLanguageChanged -= ReloadTaskTrayContextMenu;

            if (_taskTrayIcon != null)
            {
                _taskTrayIcon.Visible = false;
                _taskTrayIcon.ContextMenuStrip?.Dispose();
                _taskTrayIcon.Dispose();
                _taskTrayIcon = null;
            }
        }


        public void ReloadTaskTrayContextMenu()
        {
            Execute.OnUIThreadSync(() =>
            {
                // rebuild TaskTrayContextMenu while language changed
                if (_taskTrayIcon == null) return;

                var exit = new System.Windows.Forms.ToolStripMenuItem(IoC.Translate("Exit"));
                exit.Click += (sender, args) =>
                {
                    TaskTrayDispose();
                    App.Close();
                };

                var previousMenu = _taskTrayIcon.ContextMenuStrip;
                var menu = new TrayMenu(IsDarkTrayTheme);
                _taskTrayIcon.ContextMenuStrip = menu;
                // A language/recent-list change can originate inside the old menu's click handler.
                if (previousMenu != null)
                    Application.Current.Dispatcher.BeginInvoke(new Action(previousMenu.Dispose));
                var open = new ToolStripMenuItem($"打开 {Assert.APP_DISPLAY_NAME}");
                open.Click += (_, _) => IoC.Get<MainWindowViewModel>().ShowMe();
                menu.Items.Add(open);
                if (IoC.Get<ConfigurationService>().General.ShowRecentlySessionInTray)
                {
                    var protocolBaseViewModels = IoC.Get<GlobalData>().VmItemList
                        .OrderBy(x => ConnectionOrder(x.Server.DisplayName))
                        .ThenBy(x => x.Server.DisplayName, StringComparer.CurrentCultureIgnoreCase).Take(20).ToArray();
                    if (protocolBaseViewModels.Any())
                    {
                        menu.Items.Add(new ToolStripSeparator());
                        for (var i = 0; i < 20 && i < protocolBaseViewModels.Length; i++)
                        {
                            var protocolBaseViewModel = protocolBaseViewModels[i];
                            //var text = $"{(i + 1):D2}. {protocolBaseViewModel.Server.DisplayName} - {protocolBaseViewModel.Server.SubTitle}";
                            var text = $"{protocolBaseViewModel.Server.DisplayName}";
                            if (text.Length > 30)
                            {
                                text = text.Substring(0, 30) + "...";
                            }

                            var button = new System.Windows.Forms.ToolStripMenuItem(text);
                            button.Click += (sender, args) =>
                            {
                                GlobalEventHelper.OnRequestServerConnect?.Invoke(protocolBaseViewModel.Server, fromView: "Tray");
                            };
                            _taskTrayIcon.ContextMenuStrip.Items.Add(button);
                        }
                    }
                }




                menu.Items.Add(new ToolStripSeparator());
                _taskTrayIcon.ContextMenuStrip.Items.Add(exit);
            });

            // After startup and initializing our application and when closing our window and minimize the application to tray we free memory with the following line:
            System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet = System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet;
        }
        internal static int ConnectionOrder(string name)
        {
            if (name.Equals("Office PC", StringComparison.OrdinalIgnoreCase)) return 0;
            if (name.StartsWith("Server 120 ", StringComparison.OrdinalIgnoreCase)) return 1;
            if (name.StartsWith("Server 140 ", StringComparison.OrdinalIgnoreCase)) return 2;
            if (name.StartsWith("Server 141 ", StringComparison.OrdinalIgnoreCase)) return 3;
            return 4;
        }

        private static bool IsDarkTrayTheme()
        {
            var color = System.Drawing.ColorTranslator.FromHtml(IoC.Get<ThemeService>().CurrentTheme.BackgroundColor);
            return color.GetBrightness() < 0.5f;
        }
        #endregion

    }
}
