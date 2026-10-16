using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using _1RM.Service;
using _1RM.View.Utils;

namespace _1RM.Utils;

// Keeps native-call button results/defaults while using the application's dialog shell.
public static class ThemedMessageBox
{
    public static MessageBoxResult Show(string text, string caption, MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None,
        IDictionary<MessageBoxResult, string>? buttonLabels = null)
        => Show(null, text, caption, buttons, icon, defaultResult, buttonLabels);

    public static MessageBoxResult Show(Window? owner, string text, string caption, MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None,
        IDictionary<MessageBoxResult, string>? buttonLabels = null)
    {
        var app = Application.Current;
        if (app != null && !app.Dispatcher.CheckAccess())
            return app.Dispatcher.Invoke(() => Show(owner, text, caption, buttons, icon, defaultResult, buttonLabels));
        // Startup resource failures can occur before any application theme exists.
        if (app?.TryFindResource("BackgroundBrush") is not Brush)
            return owner == null ? System.Windows.MessageBox.Show(text, caption, buttons, icon, defaultResult)
                : System.Windows.MessageBox.Show(owner, text, caption, buttons, icon, defaultResult);
        if (string.IsNullOrWhiteSpace(caption) || caption == Assert.APP_DISPLAY_NAME)
            caption = UiText.Get(icon == MessageBoxImage.Error ? "Error" : icon == MessageBoxImage.Warning ? "Warning" : "Info");
        var window = CreateDialog(text, caption, buttons, icon, defaultResult, buttonLabels);
        var model = (MessageBoxPageViewModel)window.DataContext;
        owner ??= app.Windows.OfType<Window>().FirstOrDefault(w => w != window && w.IsActive && w.IsVisible)
                  ?? (app.MainWindow?.IsVisible == true ? app.MainWindow : null);
        window.Owner = owner;
        if (owner == null) window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        if (MessageBoxPageViewModel.SoundMapping.TryGetValue(icon, out var sound)) sound?.Play();
        window.ShowDialog();
        return model.ClickedButton;
    }

    internal static MessageBoxView CreateDialog(string text, string caption, MessageBoxButton buttons,
        MessageBoxImage icon, MessageBoxResult defaultResult, IDictionary<MessageBoxResult, string>? buttonLabels = null)
    {
        var window = new MessageBoxView();
        var model = new MessageBoxPageViewModel();
        model.Setup(text, caption, buttons, icon, defaultResult, buttonLabels: buttonLabels, onButtonClicked: window.Close);
        window.DataContext = model;
        window.Title = model.DisplayName;
        var body = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
        var message = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        message.SetResourceReference(TextBlock.ForegroundProperty, "BackgroundTextBrush");
        body.Children.Add(new ScrollViewer { Content = message, MaxHeight = 380, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        Button? defaultAction = null;
        foreach (var choice in model.ButtonList!)
        {
            var action = new Button { Content = choice.Label, MinWidth = 88, MinHeight = 32, Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(8, 0, 0, 0), IsDefault = Equals(choice, model.DefaultButton) };
            action.SetResourceReference(FrameworkElement.StyleProperty, "ButtonPrimaryStyle");
            _1RM.Controls.KeyboardAction.Enable(action);
            if (action.IsDefault) defaultAction = action;
            action.Click += (_, _) => model.ButtonClicked(choice.Value); actions.Children.Add(action);
        }
        body.Children.Add(actions); window.DialogContent = body;
        if (defaultAction != null)
        {
            System.Windows.Input.FocusManager.SetFocusedElement(window, defaultAction);
            window.ContentRendered += (_, _) => defaultAction.Focus();
        }
        return window;
    }
}
