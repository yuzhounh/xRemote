using System.Reflection;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

internal static class Program
{
    [STAThread] static void Main(string[] args)
    {
        string bin = Path.GetFullPath(args[0]);
        AssemblyLoadContext.Default.Resolving += (_, name) => File.Exists(Path.Combine(bin, name.Name + ".dll"))
            ? AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(bin, name.Name + ".dll")) : null;
        var assembly = Assembly.LoadFrom(Path.Combine(bin, "1Remote.dll"));
        var app = new Application();
        foreach (var uri in new[] { "/Shawn.Utils.WpfResources;component/Converter/Converter.xaml", "/Shawn.Utils.WpfResources;component/Theme/Basic/Default.xaml",
            "/Shawn.Utils.WpfResources;component/Theme/DefaultTheme.xaml", "/1Remote;component/Resources/Converter/Converter.xaml", "/1Remote;component/Resources/Icons/SVG.xaml",
            "/1Remote;component/Resources/Theme/SettingsTextFallback.xaml", "/1Remote;component/Resources/Languages/en-us.xaml", "/1Remote;component/Resources/Languages/zh-cn.xaml" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) });
        int passed = 0;
        void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine("PASS: " + label); passed++; }
        var configType = assembly.GetType("_1RM.Utils.RdpFile.RdpConfig")!;
        var config = Activator.CreateInstance(configType, "fixture", "127.0.0.1", "", "", "AutoReconnection enabled:i:1\nautoreconnection enabled:i:1")!;
        configType.GetProperty("ForceDisableAutoReconnect")!.SetValue(config, true);
        var lines = config.ToString()!.Split('\n').Select(s => s.Trim()).Where(s => s.StartsWith("autoreconnection enabled:i:", StringComparison.OrdinalIgnoreCase)).ToArray();
        Check(lines.SequenceEqual(new[] { "autoreconnection enabled:i:0" }), "managed .rdp file cannot re-enable reconnect through additional settings");

        var vmType = assembly.GetType("_1RM.View.ProtocolBaseViewModel")!;
        var vm = RuntimeHelpers.GetUninitializedObject(vmType);
        var rdp = Activator.CreateInstance(assembly.GetType("_1RM.Model.Protocol.RDP")!)!;
        vmType.GetField("_server", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm, rdp);
        var setRemote = vmType.GetMethod("SetRemoteSessionStatus", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var setNetwork = vmType.GetMethod("SetConnectionStatus", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var statusType = setRemote.GetParameters()[0].ParameterType;
        object Snapshot(string sessions) => JsonSerializer.Deserialize("{\"machine\":\"fixture\",\"observedAt\":\"2026-10-15T12:00:00Z\",\"consoleId\":1,\"privacyMode\":\"lock\",\"displayStatus\":\"fixture\",\"sessions\":" + sessions + "}", statusType, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        void Set(object? snapshot) { setRemote.Invoke(vm, new[] { snapshot }); setNetwork.Invoke(vm, new object?[] { true, "fixture", Array.Empty<object>() }); }
        Set(null);
        Check((string)vmType.GetProperty("ConnectionStatusText")!.GetValue(vm)! == "在线"
            && (string)vmType.GetProperty("CardConnectionStatusText")!.GetValue(vm)! == "在线"
            && (string)vmType.GetProperty("ConnectionStatusColor")!.GetValue(vm)! == "#34D399", "reachable device stays green and online when remote-use information is unknown");
        Check(((string)vmType.GetProperty("ConnectionStatusHint")!.GetValue(vm)!).Contains("远程使用情况未知"), "unavailable remote-use information is explained separately");
        setNetwork.Invoke(vm, new object?[] { null, "fixture", Array.Empty<object>() });
        Check((string)vmType.GetProperty("ConnectionStatusText")!.GetValue(vm)! == "检测中"
            && (string)vmType.GetProperty("ConnectionStatusColor")!.GetValue(vm)! == "#FBBF24", "unknown reachability is yellow while checking");
        Set(Snapshot("[]"));
        Check((string)vmType.GetProperty("ConnectionStatusText")!.GetValue(vm)! == "在线", "verified device without active remote use remains online");
        Set(Snapshot("[{\"session\":{\"id\":2,\"user\":\"fixture\",\"client\":\"office\",\"protocol\":2,\"state\":0,\"locked\":false,\"logonTime\":100,\"connectTime\":200},\"ticket\":\"fixture\"}]"));
        Check((string)vmType.GetProperty("ConnectionStatusText")!.GetValue(vm)! == (string)app.FindResource("Shell.Remote"), "active remote snapshot changes device status");
        setNetwork.Invoke(vm, new object?[] { false, "fixture", Array.Empty<object>() });
        Check((string)vmType.GetProperty("ConnectionStatusText")!.GetValue(vm)! == "不可达"
            && (string)vmType.GetProperty("CardConnectionStatusColor")!.GetValue(vm)! == "#F87171", "unreachable RDP is not disguised by known remote-use information");
        Set(null);

        foreach (string type in new[] { "_1RM.Controls.ServerCardItem", "_1RM.Controls.ServerLineItem" })
        {
            var item = (UserControl)Activator.CreateInstance(assembly.GetType(type)!)!;
            item.DataContext = vm; item.Measure(new Size(800, 160)); item.Arrange(new Rect(0, 0, 800, 160)); item.UpdateLayout();
            var action = Descendants(item).OfType<Button>().Single(b => BindingOperations.GetBinding(b, Button.CommandProperty)?.Path?.Path == "CmdManageRemoteSessions");
            Check(action.Command != null && action.MinHeight >= 40, type + " has bound session action and touch target");
            if (type.EndsWith("ServerLineItem"))
            {
                var text = Descendants(action).OfType<TextBlock>().Single(t => BindingOperations.GetBinding(t, TextBlock.TextProperty)?.Path?.Path == "ConnectionStatusText");
                var dot = Descendants(action).OfType<System.Windows.Shapes.Ellipse>().Single();
                double center = text.TranslatePoint(new Point(0, text.ActualHeight / 2), item).Y;
                double dotCenter = dot.TranslatePoint(new Point(0, dot.ActualHeight / 2), item).Y;
                var name = Descendants(item).OfType<TextBlock>().Single(t => BindingOperations.GetBinding(t, TextBlock.TextProperty)?.Path?.Path == "Server.DisplayName");
                double nameCenter = name.TranslatePoint(new Point(0, name.ActualHeight / 2), item).Y;
                var protocol = Descendants(item).OfType<TextBlock>().Single(t => BindingOperations.GetBinding(t, TextBlock.TextProperty)?.Path?.Path == "Server.ProtocolDisplayName");
                var icon = Descendants(item).OfType<Image>().Single(i => Grid.GetColumn(i) == 2);
                Check(Math.Abs(text.ActualHeight - text.DesiredSize.Height) < 0.1
                    && Math.Abs(center - dotCenter) < 0.1 && Math.Abs(center - nameCenter) < 0.1
                    && Math.Abs(center - protocol.TranslatePoint(new Point(0, protocol.ActualHeight / 2), item).Y) < 0.1
                    && Math.Abs(center - icon.TranslatePoint(new Point(0, icon.ActualHeight / 2), item).Y) < 0.1,
                    "status text, dot, device icon, name and protocol share vertical center without stretched text");
            }
        }
        var window = (Window)Activator.CreateInstance(assembly.GetType("_1RM.View.RemoteSessionsWindow")!, "127.0.0.1", "fixture")!;
        Check(window.Content is DockPanel, "session management window constructs without opening or connecting"); window.Close();
        Console.WriteLine($"{passed} UI/config checks passed; no windows shown; no remote connection opened.");
        app.Shutdown();
    }
    static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
