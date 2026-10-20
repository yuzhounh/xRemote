using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using XRemote.Control;
using XRemote.ControlHost;

if (args.Length == 3 && args[0] == "--display")
    return DisplayPower.RunHelper(args[1] == "off", int.Parse(args[2]));
if (args.Length == 5 && args[0] == "--init")
{
    PairingSetup.Create(args[1], args[2], int.Parse(args[3]), args[4]);
    Console.WriteLine("配置已创建；配对文件含管理密钥，仅导入到自己的客户端。");
    return 0;
}
if (args.Length == 1 && args[0] == "--inspect")
{
    Console.WriteLine(JsonSerializer.Serialize(new WindowsSessions().Read(), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    return 0;
}
var at = Array.IndexOf(args, "--config");
var configPath = at >= 0 && at + 1 < args.Length ? Path.GetFullPath(args[at + 1])
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "xRemote", "Control", "server.json");
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var config = JsonSerializer.Deserialize<HostConfig>(File.ReadAllText(configPath), json) ?? throw new InvalidOperationException("缺少服务配置");
if (config.Token.Length != 64 || !config.Token.All(Uri.IsHexDigit) || config.PrivacyMode is not ("black" or "lock")
    || !IPAddress.TryParse(config.BindAddress, out var bind) || IPAddress.Any.Equals(bind) || IPAddress.IPv6Any.Equals(bind)
    || config.Port is < 1024 or > 65535) throw new InvalidOperationException("服务配置无效");
var certificate = X509CertificateLoader.LoadPkcs12FromFile(Path.Combine(Path.GetDirectoryName(configPath)!, config.CertificatePath), null);
bool readOnly = args.Contains("--read-only");
var coordinator = new SessionCoordinator(new WindowsSessions(), readOnly ? new NoDisplay() : new DisplayPower(), config.PrivacyMode);
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = Array.Empty<string>(), ContentRootPath = AppContext.BaseDirectory });
builder.Logging.ClearProviders();
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 4096;
    options.Limits.MaxConcurrentConnections = 16;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(5);
    options.Listen(bind, config.Port, listen => listen.UseHttps(certificate));
});
var app = builder.Build();
var expected = SHA256.HashData(Encoding.UTF8.GetBytes("Bearer " + config.Token));
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    var auth = context.Request.Headers.Authorization.ToString();
    if (auth.Length > 128 || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(auth)), expected))
    { context.Response.StatusCode = 401; return; }
    try { await next(context); }
    catch (InvalidOperationException ex) { context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { message = ex.Message }); }
    catch (System.ComponentModel.Win32Exception) { context.Response.StatusCode = 503; await context.Response.WriteAsJsonAsync(new { message = "Windows 会话操作未完成，请刷新后检查权限或设备状态" }); }
});
app.MapGet("/v1/status", () => coordinator.Status());
var remoteSoftware = new RemoteSoftwareProbe();
app.MapGet("/v1/remote-access", () => remoteSoftware.Read());
var hardware = new HardwareProbe();
app.MapGet("/v1/hardware", async (bool? refresh, CancellationToken token) =>
{
    try { return Results.Ok(await hardware.Read(refresh == true, token)); }
    catch (System.TimeoutException ex) { return Results.Json(new { message = ex.Message }, statusCode: 503); }
});
var storage = new HardwareProbe(storageOnly: true);
app.MapGet("/v1/storage", async (bool? refresh, CancellationToken token) =>
{
    try { return Results.Ok(await storage.Read(refresh == true, token)); }
    catch (System.TimeoutException ex) { return Results.Json(new { message = ex.Message }, statusCode: 503); }
});
app.MapPost("/v1/disconnect", (DisconnectRequest request) => readOnly
    ? Results.Json(new { message = "本机只读验证模式，未断开连接" }, statusCode: 423)
    : Results.Ok(coordinator.Disconnect(request)));
coordinator.Audit = message =>
{
    // No credentials, client auth tokens, session tickets or remote screen contents in logs.
    try { File.AppendAllText(Path.Combine(Path.GetDirectoryName(configPath)!, "operations.log"), $"{DateTimeOffset.UtcNow:O} {message}\n"); }
    catch (IOException) { } // Logging must not prevent local recovery after a successful disconnect.
};
if (Environment.UserInteractive || args.Contains("--console"))
{
    // Console validation never blanks displays or listens for local unlock events.
    await app.RunAsync();
}
else ServiceBase.Run(new ControlService(app, coordinator, readOnly, bind));
return 0;

public sealed record HostConfig(string BindAddress, int Port, string CertificatePath, string Token, string PrivacyMode = "black");
public sealed class NoDisplay : IDisplayPower { public void Request(bool blank) { } }

internal sealed class ControlService : ServiceBase
{
    private readonly WebApplication app;
    private readonly SessionCoordinator coordinator;
    private readonly bool readOnly;
    private readonly IPAddress bind;
    private Timer? timer;
    private int observing;
    public ControlService(WebApplication app, SessionCoordinator coordinator, bool readOnly, IPAddress bind)
    {
        this.app = app; this.coordinator = coordinator; this.readOnly = readOnly; this.bind = bind;
        ServiceName = "xRemoteControl"; CanHandleSessionChangeEvent = true; CanShutdown = true;
    }
    protected override void OnStart(string[] args)
    {
        // EasyTier may be running before its virtual address has appeared after boot.
        RequestAdditionalTime(125000);
        for (int attempt = 0; !NetworkInterface.GetAllNetworkInterfaces().SelectMany(n => n.GetIPProperties().UnicastAddresses).Any(a => a.Address.Equals(bind)); attempt++)
        {
            if (attempt >= 120) throw new InvalidOperationException("私网地址尚未就绪，等待服务恢复重试");
            Thread.Sleep(1000);
        }
        app.StartAsync().GetAwaiter().GetResult();
        if (readOnly) return;
        timer = new Timer(_ =>
        {
            if (Interlocked.Exchange(ref observing, 1) != 0) return;
            try { coordinator.Observe(); }
            catch (Exception) { /* State queries stay available; never crash a service over display power. */ }
            finally { Interlocked.Exchange(ref observing, 0); }
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
    }
    protected override void OnSessionChange(SessionChangeDescription change)
    {
        if (readOnly) return;
        if (change.Reason is not (SessionChangeReason.SessionUnlock or SessionChangeReason.SessionLogon)) return;
        _ = Task.Run(async () =>
        {
            // SCM notifications can precede WTS' updated lock flag.
            for (int i = 0; i < 3; i++)
            {
                try { coordinator.LocalAuthenticated(change.SessionId); } catch (Exception) { }
                await Task.Delay(500);
            }
        });
    }
    protected override void OnStop()
    {
        timer?.Dispose();
        try { coordinator.Wake(); } catch (Exception) { }
        app.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
    }
    protected override void OnShutdown() => OnStop();
}
