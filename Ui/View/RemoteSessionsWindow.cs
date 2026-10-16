using System;
using System.Linq;
using MessageBox = _1RM.Utils.ThemedMessageBox;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using _1RM.Service;
using Microsoft.Win32;
using XRemote.Control;

namespace _1RM.View;

public sealed class RemoteSessionsWindow : Window
{
    private readonly string address;
    private readonly StackPanel sessions = new();
    private readonly TextBlock message = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 12) };
    private readonly Button refresh = new();
    private bool busy;
    public RemoteSessionsWindow(string address, string name)
    {
        this.address = address; Width = 620; Height = 450; MinWidth = 440;
        UiText.BindComputed(this, TitleProperty, () => name + " · " + UiText.Get("Remote.Title"));
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetResourceReference(BackgroundProperty, "PrimaryMidBrush");
        SetResourceReference(ForegroundProperty, "PrimaryTextBrush");
        SetResourceReference(FontFamilyProperty, "GlobalFontFamily");
        SetResourceReference(FontSizeProperty, "GlobalFontSizeBody");
        var buttonText = new FrameworkElementFactory(typeof(TextBlock));
        buttonText.SetBinding(TextBlock.TextProperty, new Binding());
        buttonText.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        buttonText.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
        var buttonStyle = new Style(typeof(Button), TryFindResource("ButtonPrimaryStyle") as Style);
        buttonStyle.Setters.Add(new Setter(MinHeightProperty, 32.0));
        buttonStyle.Setters.Add(new Setter(MinWidthProperty, 88.0));
        buttonStyle.Setters.Add(new Setter(MaxWidthProperty, 360.0));
        buttonStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(14, 6, 14, 6)));
        buttonStyle.Setters.Add(new Setter(ContentControl.ContentTemplateProperty, new DataTemplate { VisualTree = buttonText }));
        Resources[typeof(Button)] = buttonStyle;
        var panel = new DockPanel { Margin = new Thickness(20) }; Content = panel;
        var header = new StackPanel(); DockPanel.SetDock(header, Dock.Top); panel.Children.Add(header);
        var heading = new TextBlock { FontSize = 20, TextWrapping = TextWrapping.Wrap };
        UiText.Bind(heading, TextBlock.TextProperty, "Remote.Header");
        header.Children.Add(heading); header.Children.Add(message);
        var buttons = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        refresh.Margin = new Thickness(0, 0, 8, 8);
        refresh.Click += async (_, _) => await Reload(); buttons.Children.Add(refresh);
        UiText.Bind(refresh, ContentControl.ContentProperty, "Remote.Refresh");
        var import = new Button { Margin = new Thickness(0, 0, 8, 8) };
        UiText.Bind(import, ContentControl.ContentProperty, "Remote.ImportPairing");
        import.Click += async (_, _) =>
        {
            var picker = new OpenFileDialog { Filter = UiText.Get("Remote.PairingFilter") + "|*.json" };
            if (picker.ShowDialog(this) != true) return;
            try { DeviceControlClient.Import(address, picker.FileName); await Reload(); }
            catch (Exception) { UiText.Bind(message, TextBlock.TextProperty, "Remote.PairingFailed"); }
        };
        buttons.Children.Add(import); header.Children.Add(buttons);
        panel.Children.Add(new ScrollViewer { Content = sessions, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Loaded += async (_, _) => await Reload();
    }
    private async System.Threading.Tasks.Task Reload()
    {
        if (busy) return; busy = true; refresh.IsEnabled = false; sessions.Children.Clear(); UiText.Bind(message, TextBlock.TextProperty, "Remote.Querying");
        try
        {
            var state = await DeviceControlClient.Status(address);
            UiText.BindComputed(message, TextBlock.TextProperty, () => UiText.Get("Remote.Summary", state.Machine,
                state.ObservedAt.ToLocalTime().ToString("HH:mm:ss"), DisplayStatusText(state.DisplayStatus)));
            if (!state.Sessions.Any())
            {
                var empty = new TextBlock(); UiText.Bind(empty, TextBlock.TextProperty, "Remote.Empty"); sessions.Children.Add(empty);
            }
            foreach (var row in state.Sessions)
            {
                var item = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
                var s = row.Session;
                var labelKey = s.IsRemoteActive ? "Shell.Remote" : s.State == 4 ? "Remote.Disconnected" : s.Protocol == 0 ? "Remote.LocalSession" : "Remote.OtherSession";
                var label = new TextBlock { FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
                UiText.BindComputed(label, TextBlock.TextProperty, () => UiText.Get(labelKey) + " · " + s.User);
                item.Children.Add(label);
                var origin = new TextBlock { TextWrapping = TextWrapping.Wrap };
                UiText.BindComputed(origin, TextBlock.TextProperty, () => UiText.Get("Remote.Origin", string.IsNullOrEmpty(s.Client) ? UiText.Get("Remote.LocalUnknown") : s.Client, s.Id));
                item.Children.Add(origin);
                if (s.IsRemoteActive && row.Ticket != null)
                {
                    var stop = new Button { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0) };
                    UiText.Bind(stop, ContentControl.ContentProperty, "Remote.Disconnect");
                    stop.Click += async (_, _) =>
                    {
                        if (busy || MessageBox.Show(this, UiText.Get("Remote.ConfirmDisconnect", s.Client, s.User), UiText.Get("Remote.Disconnect"), MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
                        busy = true; stop.IsEnabled = false;
                        try { await DeviceControlClient.Disconnect(address, row); }
                        catch (Exception) { MessageBox.Show(this, UiText.Get("Remote.DisconnectFailed"), "xRemote"); }
                        finally { busy = false; await Reload(); }
                    };
                    item.Children.Add(stop);
                }
                sessions.Children.Add(item);
            }
        }
        catch (Exception) { UiText.Bind(message, TextBlock.TextProperty, "Remote.Unknown"); }
        finally { busy = false; refresh.IsEnabled = true; }
    }

    internal static string DisplayStatusText(string status) => UiText.Get(status switch
    {
        "系统锁屏" => "Remote.DisplayLocked",
        "息屏不可用，保留 Windows 锁屏" => "Remote.DisplayUnavailable",
        "已请求息屏；本机按键可唤醒 Windows 登录界面" => "Remote.DisplayBlanked",
        _ => status
    });
}
