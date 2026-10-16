using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using _1RM;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.View;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (_, name) =>
            File.Exists(Path.Combine(AppContext.BaseDirectory, name.Name + ".dll"))
                ? System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory, name.Name + ".dll")) : null;
        try { Run(); }
        catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "fixture-start-error.txt"), ex.ToString()); }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static void Run()
    {
        // A separate entry point bypasses the daily instance's single-instance pipe.
        // All profile files and sample connections live beside this disposable executable.
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        File.WriteAllText("FORCE_INTO_PORTABLE_MODE", "");
        if (!File.Exists("1Remote.json"))
            File.WriteAllText("1Remote.json", """
                {"General":{"CurrentLanguageCode":"zh-cn","DoNotCheckNewVersion":true,"ShowNoteFieldInListView":false},
                 "Launcher":{"LauncherEnabled":false},"Theme":{"ThemeName":"Light",
                    "PrimaryMidColor":"#FFF5F5F5","PrimaryLightColor":"#FFFFFFFF","PrimaryDarkColor":"#FFECECEC","PrimaryTextColor":"#FF303030",
                    "AccentMidColor":"#FF525252","AccentLightColor":"#FF6B6B6B","AccentDarkColor":"#FF3D3D3D","AccentTextColor":"#FFFFFFFF",
                    "BackgroundColor":"#FFFFFFFF","BackgroundTextColor":"#FF303030"},
                 "Engagement":{"DoNotShowAgain":true}}
                """);
        typeof(App).Assembly.GetType("_1RM.AppInitHelper")!.GetMethod("Init", BindingFlags.Static | BindingFlags.Public)!.Invoke(null, null);
        if (!File.Exists("fixture-layout-ready"))
        {
            Directory.CreateDirectory(".locality");
            File.WriteAllText(Path.Combine(".locality", ".list_view.json"), """
                {"ServerListNameWidth":220,"ServerListStatusWidth":84,"ServerListAddressWidth":450,
                 "ServerListProtocolWidth":90,"ServerListNoteWidth":220,"ServerListTagsWidth":160,
                 "ShowStatusColumn":true,"ShowAddressColumn":true,"ShowProtocolColumn":true,"ShowTagsColumn":false,"ShowNoteColumn":false}
                """);
        }
        var app = new App();
        app.InitializeComponent();
        DialogSmokeChecks.Run();
        WindowCapture.Start();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            try
            {
                var data = IoC.Get<GlobalData>();
                var config = IoC.Get<ConfigurationService>();
                if (data == null || config?.LocalDataSource == null) return;
                if (!File.Exists("fixture-layout-ready"))
                {
                    var layout = _1RM.Service.Locality.LocalityListViewService.Settings;
                    layout.ServerListNameWidth = 220; layout.ServerListStatusWidth = 84;
                    layout.ServerListAddressWidth = 450; layout.ServerListProtocolWidth = 90;
                    layout.ServerListNoteWidth = 220; layout.ServerListTagsWidth = 160;
                    layout.ShowNoteColumn = false; layout.ShowTagsColumn = false;
                    _1RM.Service.Locality.LocalityListViewService.Save();
                    File.WriteAllText("fixture-layout-ready", "standard layout");
                }
                if (data.VmItemList.Count == 0)
                {
                    ProtocolBase[] samples = {
                        new RDP { DisplayName = "示例 Windows", Address = "192.0.2.10", Port = "3389", UserName = "demo", Tags = new() { "示例" }, Note = "Windows 演示设备" },
                        new SSH { DisplayName = "示例 Linux", Address = "192.0.2.20", Port = "22", UserName = "demo", Tags = new() { "示例" }, Note = "Linux 演示设备" },
                        new SFTP { DisplayName = "示例文件传输", Address = "192.0.2.20", Port = "22", UserName = "demo", Tags = new() { "示例" }, Note = "文件传输演示" }
                    };
                    foreach (var sample in samples) data.AddServer(sample, config.LocalDataSource);
                }
                IoC.Get<MainWindowViewModel>().ShowMe(true);
                if (IoC.Get<MainWindowViewModel>().View is Window main) { main.Width = 1080; main.Height = 720; }
                timer.Stop();
            }
            catch (Exception ex) { File.WriteAllText("capture-error.txt", ex.ToString()); }
        };
        timer.Start();
        app.Run();
    }
}
