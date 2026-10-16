using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using _1RM.Service;
using _1RM.Service.Locality;
using _1RM.Utils;
using _1RM.View.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.WpfResources.Theme.Styles;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;

namespace _1RM.View
{
    public partial class MainWindowView : WindowChromeBase
    {
        public MainWindowViewModel Vm { get; }
        private readonly ConfigurationService _configurationService;
        private readonly NativeMainTitleBar _nativeTitleBar;


        public MainWindowView(MainWindowViewModel vm, LocalityService localityService, ConfigurationService configurationService)
        {
            InitializeComponent();
            Vm = vm;
            _configurationService = configurationService;
            this.DataContext = Vm;
            BindThemeMenu(PopupMenu, ThemePresetMenu, IoC.Get<Settings.Theme.ThemeSettingViewModel>());
            PopupMenu.Opened += (_, _) => Controls.CompactMenuBehavior.SynchronizeWidths(MainMenuItems);
            Title = Assert.APP_DISPLAY_NAME;
            // restore the window size from 
            this.Width = localityService.MainWindowWidth;
            this.Height = localityService.MainWindowHeight;
            this.WindowState = localityService.MainWindowState;

            this.SizeChanged += (sender, args) =>
            {
                if (ConnectionSearchBar.Visibility == Visibility.Visible) UpdateSearchLayout();
                FitDeviceDetails();
                if (this.WindowState == WindowState.Normal)
                {
                    localityService.MainWindowState = this.WindowState;
                    localityService.MainWindowHeight = this.Height;
                    localityService.MainWindowWidth = this.Width;
                    SimpleLogHelper.Debug($"Main window resize to: w = {this.Width}, h = {this.Height}");
                }
            };

            this.LocationChanged += (sender, args) =>
            {
                localityService.MainWindowTop = this.Top;
                localityService.MainWindowLeft = this.Left;
                SimpleLogHelper.Debug($"Main window move to: top = {this.Top}, left = {this.Left}");
            };

            this.StateChanged += (sender, args) =>
            {
                localityService.MainWindowState = this.WindowState;
            };

            _nativeTitleBar = new NativeMainTitleBar(this, NativeCaptionRow, WinTitleBar, CaptionMenuArea, HandleCaptionClose, ConnectionSearchBar);

            // Restore or reset window location
            if (double.IsNaN(localityService.MainWindowTop) || double.IsNaN(localityService.MainWindowLeft)
                || localityService.MainWindowTop < SystemParameters.VirtualScreenTop
                || localityService.MainWindowTop + this.Height > SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight
                || localityService.MainWindowLeft < SystemParameters.VirtualScreenLeft
                || localityService.MainWindowLeft + this.Width > SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth)
            {
                ResetLocation();
            }
            else
            {
                this.Top = localityService.MainWindowTop;
                this.Left = localityService.MainWindowLeft;
            }

        }

        private void HandleCaptionClose()
        {
            if ((_configurationService.Engagement.DoNotShowAgain == false || AppVersion.VersionData > _configurationService.Engagement.DoNotShowAgainVersion)
                && _configurationService.Engagement.InstallTime < DateTime.Now.AddDays(-15)
                && _configurationService.Engagement.LastRequestRatingsTime < DateTime.Now.AddDays(-60)
                && _configurationService.Engagement.ConnectCount > 100
               )
            {
                // 显示“请求应用的评分和评价”页面 https://docs.microsoft.com/zh-cn/windows/uwp/monetize/request-ratings-and-reviews
                MaskLayerController.ShowMask(IoC.Get<RequestRatingViewModel>(), Vm);
                return;
            }
            Vm.HideMe();
#if DEBUG
            App.Close();
#else
            switch (IoC.Get<ConfigurationService>().General.CloseButtonBehavior)
            {
                case (int)GeneralConfig.EnumCloseButtonBehavior.Exit:
                    App.Close();
                    break;
                case (int)GeneralConfig.EnumCloseButtonBehavior.Minimize:
                    // Minimize to system tray - just hide
                default:
                    break;
            }
#endif
        }

        public void ResetLocation()
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var screenEx = ScreenInfoEx.GetCurrentScreenBySystemPosition(ScreenInfoEx.GetMouseSystemPosition());
            // Check the current screen size
            this.WindowState = WindowState.Normal;
            if (this.Width > screenEx.VirtualWorkingArea.Width)
                this.Width = Math.Min(screenEx.VirtualWorkingArea.Width * 0.8, this.Width * 0.8);
            if (this.Height > screenEx.VirtualWorkingArea.Height)
                this.Height = Math.Min(screenEx.VirtualWorkingArea.Height * 0.8, this.Height * 0.8);
            // Place the window in the center of the current screen
            this.Top = screenEx.VirtualWorkingAreaCenter.Y - this.Height / 2;
            this.Left = screenEx.VirtualWorkingAreaCenter.X - this.Width / 2;
        }

        public void EnsureVisibleLocation()
        {
            var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            if (bounds.IsEmpty || !double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y))
            {
                ResetLocation();
                return;
            }
            bool titleVisible = System.Windows.Forms.Screen.AllScreens.Any(screen =>
            {
                var area = new ScreenInfoEx(screen).VirtualWorkingArea;
                var visible = Rect.Intersect(new Rect(bounds.X, bounds.Y, bounds.Width, 40),
                    new Rect(area.X, area.Y, area.Width, area.Height));
                return !visible.IsEmpty && visible.Width >= 80 && visible.Height >= 20;
            });
            if (!titleVisible) ResetLocation();
        }

        internal static void BindThemeMenu(System.Windows.Controls.Primitives.Popup popup,
            System.Windows.Controls.MenuItem menu, Settings.Theme.ThemeSettingViewModel settings)
        {
            // Build before first expansion and refresh through the containing popup's
            // direct event, rather than filtering a routed submenu event's Source.
            PopulateThemeMenu(menu, settings);
            popup.Opened += (_, _) => PopulateThemeMenu(menu, settings);
        }

        internal static void PopulateThemeMenu(System.Windows.Controls.MenuItem menu, Settings.Theme.ThemeSettingViewModel settings)
        {
            menu.Items.Clear();
            foreach (var name in settings.ThemeList)
            {
                var item = new System.Windows.Controls.MenuItem
                {
                    Header = new Controls.LocalizedOptionText { Value = name, ResourcePrefix = "ThemePreset." },
                    Tag = name, IsCheckable = true, IsChecked = settings.ThemeName == name
                };
                item.SetResourceReference(StyleProperty, "UniformOptionMenuItemStyle");
                var mark = new System.Windows.Shapes.Path();
                mark.SetResourceReference(StyleProperty, "MenuSelectionMarkStyle");
                mark.SetBinding(VisibilityProperty, new System.Windows.Data.Binding(nameof(item.IsChecked))
                {
                    Source = item, Converter = new System.Windows.Controls.BooleanToVisibilityConverter()
                });
                item.Icon = mark;
                item.Click += (_, _) =>
                {
                    settings.ThemeName = name;
                    foreach (System.Windows.Controls.MenuItem option in menu.Items)
                        option.IsChecked = ReferenceEquals(option, item);
                };
                menu.Items.Add(item);
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (this.ShowInTaskbar)
            {
#if !DEBUG
                // Honor the user's 'close button behavior = Exit' choice for Alt+F4 and the
                // taskbar 'Close window' command too, mirroring BtnClose.Click's Exit branch
                // instead of always hiding to the tray (which leaves a background process
                // with live sessions that the user believes is gone).
                if (IoC.Get<ConfigurationService>().General.CloseButtonBehavior == (int)GeneralConfig.EnumCloseButtonBehavior.Exit)
                {
                    Vm.HideMe(); // clears ShowInTaskbar so App.Close's internal close is not re-intercepted here
                    App.Close();
                    return;
                }
#endif
                Vm.HideMe();
                e.Cancel = true;
            }
            else
            {
                base.OnClosing(e);
            }
        }


        private void CommandFocusFilter_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            SimpleLogHelper.Debug($"CommandFocusFilter_OnExecuted");
            if (Vm.IsShownList)
            {
                if (Vm.ActiveServerViewModel.TagListViewModel == null)
                {
                    ShowConnectionSearch();
                }
                else
                {
                    Vm.ActiveServerViewModel.TagsPanelViewModel.FilterIsFocused = true;
                }
            }
        }



        private void MainWindow_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (this.DataContext is MainWindowViewModel vm)
            {
                if (Keyboard.FocusedElement is TextBox)
                {
                    //SimpleLogHelper.Debug($"Current FocusedElement is " + textBox.Name);
                }
                else if (e.Key == Key.Escape && vm.IsShownList == false)
                {
                    vm.ShowList(false);
                }
            }
        }

        private void ProcessingRing_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 等待动画实现拖拽
            if (e.ClickCount >= 2)
                return;
            WinTitleBar_OnPreviewMouseDown(sender, e);
        }


        private void ShowConnectionSearch()
        {
            CbPopForMenu.IsChecked = false;
            UpdateSearchLayout();
            ConnectionSearchBar.Visibility = Visibility.Visible;
            Vm.ActiveServerViewModel?.CalcServerVisibleAndRefresh(force: true);
            Vm.RefreshConnectionSearchSummary();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SearchConnectionsTextBox.Focus();
                SearchConnectionsTextBox.SelectAll();
            }));
        }

        private void UpdateSearchLayout()
        {
            double reserved = Math.Max(190, CaptionMenuArea.Margin.Right + 45) + 8;
            double available = ActualWidth - 2 * reserved;
            bool inCaption = available >= 240;
            System.Windows.Controls.Grid.SetRow(ConnectionSearchBar, inCaption ? 0 : 1);
            ConnectionSearchBar.VerticalAlignment = inCaption ? VerticalAlignment.Center : VerticalAlignment.Top;
            ConnectionSearchBar.Margin = inCaption ? new Thickness(0, 4, 0, 4) : new Thickness(0, 8, 0, 0);
            ConnectionSearchBar.Width = Math.Max(240, Math.Min(420, inCaption ? available : ActualWidth - 32));
        }

        private void SearchConnections_OnClick(object sender, RoutedEventArgs e)
        {
            if (!Vm.IsShownList) Vm.ShowList(false);
            ShowConnectionSearch();
        }

        private void CloseConnectionSearch_OnClick(object sender, RoutedEventArgs e)
        {
            Vm.SetMainFilterString(null, null);
            ConnectionSearchBar.Visibility = Visibility.Collapsed;
        }

        private void MainFilter_OnKeyUp(object sender, KeyEventArgs e)
        {
            // When press Esc, clear all the search keywords, but keep selected tags;
            if (e.Key != Key.Escape || sender is TextBox == false) return;
            var s = TagAndKeywordEncodeHelper.DecodeKeyword(Vm.MainFilterString);
            Vm.SetMainFilterString(s.KeyWords.Count == 0 ? null : s.TagFilterList, null);
            ConnectionSearchBar.Visibility = Visibility.Collapsed;
        }


        public override void WinTitleBar_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Vm.TopLevelViewModel != null)
                return;
            base.WinTitleBar_OnPreviewMouseDown(sender, e);
        }
    }
}
