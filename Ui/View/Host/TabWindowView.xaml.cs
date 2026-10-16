using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using _1RM.Service;
using _1RM.Service.Locality;
using _1RM.Utils;
using Shawn.Utils.Wpf.Controls;
using Stylet;
using System.Diagnostics;
using System.Linq;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using _1RM.View.Host.ProtocolHosts;
using _1RM.Model;
using Dragablz;
using _1RM.View.Utils;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace _1RM.View.Host
{
    public partial class TabWindowView
    {
        public const double TITLE_BAR_HEIGHT = 48;

        public static readonly DependencyProperty NativeCaptionHeightProperty = DependencyProperty.Register(
            nameof(NativeCaptionHeight), typeof(double), typeof(TabWindowView), new PropertyMetadata(TITLE_BAR_HEIGHT));
        public double NativeCaptionHeight
        {
            get => (double)GetValue(NativeCaptionHeightProperty);
            private set => SetValue(NativeCaptionHeightProperty, value);
        }
        public static readonly DependencyProperty NativeCaptionReservedWidthProperty = DependencyProperty.Register(
            nameof(NativeCaptionReservedWidth), typeof(double), typeof(TabWindowView), new PropertyMetadata(189.0));
        public double NativeCaptionReservedWidth
        {
            get => (double)GetValue(NativeCaptionReservedWidthProperty);
            private set => SetValue(NativeCaptionReservedWidthProperty, value);
        }
        private readonly NativeMainTitleBar _nativeTitleBar;

        private RectInt32[] LayoutNativeCaption(AppWindowTitleBar bar, double scaleX, double scaleY)
        {
            NativeCaptionHeight = bar.Height > 0 ? bar.Height / scaleY : TITLE_BAR_HEIGHT;
            double right = Math.Max(0, bar.RightInset / scaleX);
            NativeCaptionReservedWidth = right + SessionFullScreenButton.Width;
            SessionFullScreenButton.Margin = new Thickness(0, 0, right, 0);
            if (TabablzControl.HeaderSuffixContent is not Grid suffix
                || suffix.Children.OfType<Grid>().FirstOrDefault() is not { } actions
                || actions.Children.OfType<Grid>().FirstOrDefault(x => x.Tag as string == "NativeCaptionDragRegion") is not { } drag
                || drag.ActualWidth <= 0)
                return Array.Empty<RectInt32>();
            // The root grid is the extended client area, including the caption.
            var origin = drag.TranslatePoint(new Point(0, 0), (UIElement)Content);
            return new[] { new RectInt32((int)Math.Ceiling(origin.X * scaleX), 0,
                Math.Max(0, (int)Math.Floor(drag.ActualWidth * scaleX)), (int)Math.Ceiling(NativeCaptionHeight * scaleY)) };
        }

        protected readonly TabWindowViewModel Vm;
        public string Token => Vm.Token;

        private IntPtr _myHandle = IntPtr.Zero;
        private System.Windows.Interop.HwndSource? _hwndSource;
        private System.Windows.Interop.HwndSourceHook? _wndProcHook;
        private bool _separatorUpdatePending;

        private void QueueTabSeparatorUpdate()
        {
            if (_separatorUpdatePending) return;
            _separatorUpdatePending = true;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                _separatorUpdatePending = false;
                var headers = TabablzControl.GetOrderedHeaders()?.ToArray();
                if (headers == null) return;
                for (var i = 0; i < headers.Length; i++)
                    ChromeTabBackground.SetShowSeparator(headers[i], !headers[i].IsSelected
                        && !(i + 1 < headers.Length && headers[i + 1].IsSelected));
            }));
        }

        private void RecentConnections_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { ContextMenu: { } menu } button) return;
            PopulateRecentConnections(menu);
            menu.PlacementTarget = button;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private void PopulateRecentConnections(ContextMenu menu)
        {
            menu.Items.Clear();
            var orderedTabs = TabablzControl.GetOrderedHeaders()?
                .Select(x => x.DataContext as TabItemViewModel ?? x.Content as TabItemViewModel)
                .OfType<TabItemViewModel>().Where(Vm.Items.Contains).ToArray();
            var tabs = orderedTabs is { Length: > 0 } ? orderedTabs : Vm.Items.ToArray();
            if (tabs.Length > 0)
            {
                menu.Items.Add(new MenuItem { Header = _1RM.Service.UiText.Get("Shell.CurrentTabs"), IsEnabled = false });
                foreach (var tab in tabs)
                {
                    var item = new MenuItem
                    {
                        Header = tab.DisplayName,
                        ToolTip = tab.Content.ProtocolServer.SubTitle,
                        IsChecked = tab == Vm.SelectedItem,
                        Icon = new Image { Source = tab.IconImg, Width = 16, Height = 16 }
                    };
                    item.Click += (_, _) =>
                    {
                        if (!Vm.Items.Contains(tab)) return;
                        Vm.SelectedItem = tab;
                        Activate();
                    };
                    menu.Items.Add(item);
                }
            }
            var openIds = tabs.Select(x => x.Content.ProtocolServer.Id).ToHashSet();
            var recent = IoC.Get<GlobalData>().VmItemList
                .Select(x => new { Server = x.Server, Last = LocalityConnectRecorder.SuccessfulConnectTimeGet(x.Server) })
                .Where(x => x.Last > DateTime.MinValue && !openIds.Contains(x.Server.Id))
                .OrderByDescending(x => x.Last).Take(8).ToArray();
            if (recent.Length > 0)
            {
                if (menu.Items.Count > 0) menu.Items.Add(new Separator());
                menu.Items.Add(new MenuItem { Header = _1RM.Service.UiText.Get("Shell.Recent"), IsEnabled = false });
            }
            if (recent.Length == 0 && tabs.Length == 0)
                menu.Items.Add(new MenuItem { Header = _1RM.Service.UiText.Get("Shell.NoRecent"), IsEnabled = false });
            foreach (var entry in recent)
            {
                var server = entry.Server;
                var item = new MenuItem
                {
                    Header = server.DisplayName,
                    ToolTip = server.SubTitle,
                    Icon = new Image { Source = server.IconImg, Width = 16, Height = 16 }
                };
                item.Click += (_, _) => GlobalEventHelper.OnRequestServerConnect?.Invoke(
                    server, fromView: "Recent connections", assignTabToken: Vm.Token);
                menu.Items.Add(item);
            }
        }



        public TabWindowView()
        {
            InitializeComponent();
            Vm = new TabWindowViewModel(this);
            DataContext = Vm;
            _nativeTitleBar = new NativeMainTitleBar(this, TabablzControl,
                () => Vm.CmdCloseAll.Execute(null), LayoutNativeCaption);
            Vm.Items.CollectionChanged += (_, _) => QueueTabSeparatorUpdate();
            TabablzControl.AddHandler(DragablzItem.LogicalIndexChangedEvent,
                new RoutedPropertyChangedEventHandler<int>((_, _) => QueueTabSeparatorUpdate()));

            this.MinWidth = this.MinHeight = 300;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.WindowStyle = WindowStyle.SingleBorderWindow;

            Focusable = true;
            this.Loaded += (_, _) =>
            {
                // Bind to the actual prefix button after template creation. Window-level
                // routed event Source may be retargeted by the tab control/template.
                if (TabablzControl.HeaderPrefixContent is Panel prefix)
                    foreach (var button in prefix.Children.OfType<Button>().Where(x => x.Tag as string == "RecentConnections"))
                    {
                        button.Click -= RecentConnections_OnClick;
                        button.Click += RecentConnections_OnClick;
                    }
                InitWindowSizeOnLoaded();
                TimerInitOnLoaded();
                QueueTabSeparatorUpdate();
                _myHandle = new WindowInteropHelper(this).Handle;
                Keyboard.Focus(this);

                _hwndSource = System.Windows.Interop.HwndSource.FromHwnd(_myHandle);
                _wndProcHook = new System.Windows.Interop.HwndSourceHook(AdditionalWndProc);
                _hwndSource?.AddHook(_wndProcHook);

                // remember window size when size changed
                SizeChanged += (_, _) =>
                {
                    if (this.WindowState == WindowState.Normal)
                    {
                        IoC.Get<LocalityService>().TabWindowWidth = this.ActualWidth;
                        IoC.Get<LocalityService>().TabWindowHeight = this.ActualHeight;
                    }
                    SimpleLogHelper.DebugInfo($"(Window size changed) Tab size change to:W = {this.ActualWidth}, H = {this.ActualHeight}");
                };

                StateChanged += delegate
                {
                    if (this.WindowState == WindowState.Minimized)
                    {
                        Vm?.SelectedItem?.Content?.ToggleAutoResize(false);
                        return;
                    }

                    if (Vm.SelectedItem?.Content.CanResizeNow() != true)
                    {
                        return;
                    }
                    Vm?.SelectedItem?.Content?.ToggleAutoResize(true);
                    IoC.Get<LocalityService>().TabWindowState = this.WindowState;
                    SimpleLogHelper.DebugInfo($"(Window state changed) Tab size change to:W = {this.ActualWidth}, H = {this.ActualHeight}");
                };


                Closing += (_, args) =>
                {
                    if (this.GetViewModel().Items.Count > 0
                        && App.ExitingFlag == false
                        && IoC.Get<ConfigurationService>().General.ConfirmBeforeClosingSession == true
                        && false == MessageBoxHelper.Confirm(IoC.Translate("Are you sure you want to close the connection?"), ownerViewModel: Vm))
                    {
                        args.Cancel = true;
                    }
                };


                Closed += (_, _) =>
                {
                    _hwndSource?.RemoveHook(_wndProcHook);
                    TimerDispose();
                    try
                    {
                        var ids = Vm.Items.Select(x => x.Content.ConnectionId).ToArray();
                        if (ids.Length > 0)
                        {
                            IoC.Get<SessionControlService>().CloseProtocolHostAsync(ids);
                        }
                        Vm?.Dispose();
                    }
                    finally
                    {
                        DataContext = null;
                        System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet = System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet;
                    }
                };


                this.Activated += (_, _) =>
                {
                    this.StopFlashingWindow();
                    // Ensure taskbar icon is visible when window is activated (fixes Win11 issue where taskbar icon disappears when window loses focus)
                    this.ShowInTaskbar = true;
                };

                if (IoC.Get<LocalityService>().TabWindowState != System.Windows.WindowState.Minimized)
                {
                    this.WindowState = IoC.Get<LocalityService>().TabWindowState;
                }
            };
        }

        private IntPtr AdditionalWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Persist placement and restore integrated-app focus after native caption dragging.
            if (msg == 0x0232 && WindowState == WindowState.Normal) // WM_EXITSIZEMOVE
            {
                IoC.Get<LocalityService>().TabWindowTop = Top;
                IoC.Get<LocalityService>().TabWindowLeft = Left;
                if (Vm?.SelectedItem?.Content?.GetProtocolHostType() != ProtocolHostType.Integrate)
                    RunForIntegrate();
            }
            const int WM_GETICON = 0x007F;
            const int ICON_BIG = 1;
            const int ICON_SMALL = 0;
            // const int ICON_SMALL2 = 2;

            if (msg == WM_GETICON)
            {
                // Abort the GETICON message under certain conditions to
                // prevent the Windows 10 Volume Mixer from picking up the
                // session icons. Not an elegant way, but it works.

                // Experiments confirmed that the following items display
                // session icon as before (as expected).
                //   This tab, Taskbar, Alt+Tab, Win+Tab, Task Manager

                int size = wParam.ToInt32();
                int dpi = lParam.ToInt32();
                if (dpi == 0 && (size == ICON_SMALL || size == ICON_BIG))
                {
                    handled = true;  // Abort
                }
            }
            return IntPtr.Zero;
        }

        private void InitWindowSizeOnLoaded()
        {
            var screenEx = ScreenInfoEx.GetCurrentScreenBySystemPosition(ScreenInfoEx.GetMouseSystemPosition());
            var leftTopOfCurrentScreen = new Point(screenEx.VirtualWorkingArea.X, screenEx.VirtualWorkingArea.Y);
            var rightBottomOfCurrentScreen = new Point(screenEx.VirtualWorkingArea.X + screenEx.VirtualWorkingArea.Width, screenEx.VirtualWorkingArea.Y + screenEx.VirtualWorkingArea.Height);
            if (WindowState == System.Windows.WindowState.Maximized)
            {

            }
            else
            {
                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Width = IoC.Get<LocalityService>().TabWindowWidth;
                this.Height = IoC.Get<LocalityService>().TabWindowHeight;
                // check current screen size
                if (IoC.Get<LocalityService>().TabWindowTop <= leftTopOfCurrentScreen.Y - TITLE_BAR_HEIGHT                                // check if the title bar outside the screen.
                    || IoC.Get<LocalityService>().TabWindowTop > rightBottomOfCurrentScreen.Y                                             // check if the title bar outside the screen.
                    || IoC.Get<LocalityService>().TabWindowLeft > rightBottomOfCurrentScreen.X                                            // check if the title bar outside the screen.
                    || IoC.Get<LocalityService>().TabWindowLeft + IoC.Get<LocalityService>().TabWindowWidth < leftTopOfCurrentScreen.X              // check if the title bar outside the screen.
                    || IoC.Get<LocalityService>().TabWindowTop + IoC.Get<LocalityService>().TabWindowHeight / 2 < leftTopOfCurrentScreen.Y          // check if the center of tab window local in current screen
                    || IoC.Get<LocalityService>().TabWindowTop + IoC.Get<LocalityService>().TabWindowHeight / 2 > rightBottomOfCurrentScreen.Y      // check if the center of tab window local in current screen
                    || IoC.Get<LocalityService>().TabWindowLeft + IoC.Get<LocalityService>().TabWindowWidth / 2 < leftTopOfCurrentScreen.X          // check if the center of tab window local in current screen
                    || IoC.Get<LocalityService>().TabWindowLeft + IoC.Get<LocalityService>().TabWindowWidth / 2 > rightBottomOfCurrentScreen.X      // check if the center of tab window local in current screen
                   )
                {
                    // default width & height
                    if (this.Width >= screenEx.VirtualWorkingArea.Width)
                        this.Width = Math.Min(screenEx.VirtualWorkingArea.Width * 0.8, this.Width * 0.8);
                    if (this.Height >= screenEx.VirtualWorkingArea.Height)
                        this.Height = Math.Min(screenEx.VirtualWorkingArea.Height * 0.8, this.Height * 0.8);
                    // default top & left
                    this.Top = screenEx.VirtualWorkingAreaCenter.Y - this.Height / 2;
                    this.Left = screenEx.VirtualWorkingAreaCenter.X - this.Width / 2;
                }
                else
                {
                    this.Top = IoC.Get<LocalityService>().TabWindowTop;
                    this.Left = IoC.Get<LocalityService>().TabWindowLeft;
                }
            }
        }

        protected virtual void TabablzControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            QueueTabSeparatorUpdate();
            if (Vm?.SelectedItem?.Content != null)
            {
                this.Icon = IoC.Get<ConfigurationService>().General.ShowSessionIconInSessionWindow ?
                    Vm.SelectedItem.Content.ProtocolServer.IconImg : null;
            }
        }

        public TabWindowViewModel GetViewModel()
        {
            return Vm;
        }

        public Size GetTabContentSize(bool withoutBorderColor)
        {
            var size = new Size(800, 600);
            Execute.OnUIThreadSync(() =>
            {
                if (!this.IsLoaded || TabablzControl == null) return;
                Debug.Assert(this.Resources["TabContentBorderWithColor"] != null);
                Debug.Assert(this.Resources["TabContentBorderWithOutColor"] != null);
                var tabContentBorderWithColor = (Thickness)this.Resources["TabContentBorderWithColor"];
                var tabContentBorderWithOutColor = (Thickness)this.Resources["TabContentBorderWithOutColor"];

                double actualWidth = TabablzControl.ActualWidth;
                double actualHeight = TabablzControl.ActualHeight;
                double border1 = withoutBorderColor ? tabContentBorderWithOutColor.Left + tabContentBorderWithOutColor.Right : tabContentBorderWithColor.Left + tabContentBorderWithColor.Right;
                double border2 = withoutBorderColor ? tabContentBorderWithOutColor.Top + tabContentBorderWithOutColor.Bottom : tabContentBorderWithColor.Top + tabContentBorderWithColor.Bottom;
                size.Width = actualWidth - border1;
                size.Height = actualHeight - NativeCaptionHeight - border2;
            });
            return size;
        }



        private void TabablzControl_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var t = sender.GetType();
            SimpleLogHelper.DebugWarning(t);
            // focus to be on the integrated exe after clicking on the WPF window.
            RunForIntegrate();
        }

    }
}
