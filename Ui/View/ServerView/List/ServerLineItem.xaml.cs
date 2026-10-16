using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using Shawn.Utils.Wpf;
using _1RM.Model;
using _1RM.View;
using _1RM.View.ServerView;
using Shawn.Utils;

namespace _1RM.Controls
{
    /// <summary>
    /// Interaction logic for ServerLineItem.xaml
    /// </summary>
    public partial class ServerLineItem : UserControl
    {
        public static readonly DependencyProperty ProtocolServerViewModelProperty =
            DependencyProperty.Register("ProtocolBaseViewModel", typeof(ProtocolBaseViewModel), typeof(ServerLineItem),
                new PropertyMetadata(null, new PropertyChangedCallback(OnDataChanged)));

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var value = (ProtocolBaseViewModel)e.NewValue;
            ((ServerLineItem)d).DataContext = value;
        }

        public ProtocolBaseViewModel? ProtocolBaseViewModel
        {
            get => GetValue(ProtocolServerViewModelProperty) as ProtocolBaseViewModel;
            set => SetValue(ProtocolServerViewModelProperty, value);
        }


        public ServerLineItem()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PopupCardSettingMenu.Closed += PopupCardSettingMenuOnClosed;
            if (ProtocolBaseViewModel?.HoverNoteDisplayControl is { } note)
                note.IsBriefNoteShown = _1RM.Service.Locality.LocalityListViewService.Settings.ShowNoteColumn
                    ?? IoC.Get<_1RM.Service.ConfigurationService>().General.ShowNoteFieldInListView;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Unsubscribe from event to prevent memory leak
            PopupCardSettingMenu.Closed -= PopupCardSettingMenuOnClosed;
        }

        private void PopupCardSettingMenuOnClosed(object? sender, EventArgs e)
        {
            ProtocolBaseViewModel?.ClearActions();
        }

        private void BtnSettingMenu_OnClick(object sender, RoutedEventArgs e)
        {
            ProtocolBaseViewModel?.BuildActions();
            PopupCardSettingMenu.IsOpen = true;
        }

        private void ServerMenuButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button { CommandParameter: ProtocolAction afs })
            {
                afs.Run();
            }
            PopupCardSettingMenu.IsOpen = false;
        }

        private void ItemsCheckBox_OnClick(object sender, RoutedEventArgs e)
        {
            ServerListPageView.ItemsCheckBox_OnClick_Static(sender, e);
        }

        private Point rowPress;
        private bool connectOnRelease;
        private long lastConnect;
        private static bool IsRowAction(DependencyObject? source) => source != null &&
            (source is ButtonBase or TextBoxBase or Thumb
             || MyVisualTreeHelper.VisualUpwardSearch<ButtonBase>(source) != null
             || MyVisualTreeHelper.VisualUpwardSearch<TextBoxBase>(source) != null
             || MyVisualTreeHelper.VisualUpwardSearch<Thumb>(source) != null);
        private void Row_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            rowPress = e.GetPosition(this);
            connectOnRelease = e.ClickCount == 1 && Keyboard.Modifiers == ModifierKeys.None
                && !IsRowAction(e.OriginalSource as DependencyObject);
        }
        private void Row_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            bool launch = connectOnRelease; connectOnRelease = false;
            var release = e.GetPosition(this);
            if (!launch || IsRowAction(e.OriginalSource as DependencyObject) || Keyboard.Modifiers != ModifierKeys.None
                || Math.Abs(release.X - rowPress.X) >= SystemParameters.MinimumHorizontalDragDistance
                || Math.Abs(release.Y - rowPress.Y) >= SystemParameters.MinimumVerticalDragDistance
                || Environment.TickCount64 - lastConnect < 700) return;
            lastConnect = Environment.TickCount64;
            var command = ProtocolBaseViewModel?.CmdConnServer;
            if (command?.CanExecute(null) == true) { command.Execute(null); e.Handled = true; }
        }

        private void UIElement_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // stop right click edit 
            if (e.ChangedButton == MouseButton.Right)
            {
                e.Handled = true;
            }
        }
    }
}
