using XRemote.Control;
using XRemote.ControlHost;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using _1RM.Service;

int passed = 0;
void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); passed++; }
bool Refused(Action operation) { try { operation(); return false; } catch (InvalidOperationException) { return true; } }
RemoteSession Remote(int id, string user = "PC\\owner", string client = "office") => new(id, user, client, 2, 0, false, 100, 200);
RemoteSession Local(bool locked = false) => new(1, "PC\\owner", "", 0, 0, locked, 10, 20);

var backend = new FakeBackend { Rows = [Local(true), Remote(2), Remote(3, "PC\\other")] };
var display = new FakeDisplay();
var coordinator = new SessionCoordinator(backend, display, "black");
var selected = coordinator.Status().Sessions.Single(s => s.Session.Id == 2);
coordinator.Disconnect(new(2, selected.Ticket!));
Check(backend.Disconnected.SequenceEqual([2]) && backend.Rows.Single(s => s.Id == 3).IsRemoteActive, "disconnect only selected session; other work retained");
Check(Refused(() => coordinator.Disconnect(new(2, selected.Ticket!))), "ticket replay refused");
selected = coordinator.Status().Sessions.Single(s => s.Session.Id == 3);
backend.Rows = [Local(true), Remote(3, "PC\\other", "replacement")];
Check(Refused(() => coordinator.Disconnect(new(3, selected.Ticket!))) && backend.Disconnected.Count == 1, "reused session id/client change refused");
Check(coordinator.Status().Sessions.Single(s => s.Session.Id == 1).Ticket == null, "no action ticket for local session");
backend.Rows = [Local(true), Remote(2), Remote(3, "PC\\other")]; backend.Disconnected.Clear();
coordinator.LocalAuthenticated(1);
Check(backend.Disconnected.Count == 0, "locked console cannot reclaim");
coordinator.LocalAuthenticated(2);
Check(backend.Disconnected.Count == 0, "remote unlock cannot impersonate local authentication");
backend.Rows = [Local(), Remote(2), Remote(3, "PC\\other")];
coordinator.LocalAuthenticated(1);
Check(backend.Disconnected.SequenceEqual([2]) && backend.Rows.Single(s => s.Id == 3).IsRemoteActive && display.Wakes > 0, "authenticated physical console reclaims own account only");
backend.Rows = [Local(true), Remote(2)]; display.Blanks = 0;
coordinator = new SessionCoordinator(backend, display, "black"); coordinator.Observe();
Check(display.Blanks == 0, "service start never blanks existing work");
backend.Rows = [Local(true)]; coordinator.Observe(); backend.Rows = [Local(true), Remote(4)]; coordinator.Observe();
Check(display.Blanks == 1, "new remote connection blanks only with confirmed locked console");
backend.Rows = [Local()]; coordinator.Observe(); backend.Rows = [Local(), Remote(5)]; coordinator.Observe();
Check(display.Blanks == 1, "unlocked or unknown console never blanked");
backend.Rows = [Local(true), Remote(5)]; coordinator.Observe();
Check(display.Blanks == 2, "delayed Windows lock transition still handles a new remote connection");
var native = new WindowsSessions().Read();
Check(native.Count >= 1 && native.All(s => s.Protocol is 0 or 2 && s.LogonTime >= 0), "real Windows WTS layout and read-only enumeration");

if (args.Length is < 1 or > 2) throw new ArgumentException("Pass the repository root and optional published host path for isolated TLS/client integration checks");
var repo = Path.GetFullPath(args[0]);
var sandbox = Path.Combine(repo, "artifacts", "control-review", "validation-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(sandbox);
var acl = new DirectorySecurity(); acl.SetAccessRuleProtection(true, false);
acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl,
    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
new DirectoryInfo(sandbox).SetAccessControl(acl);
var configDir = Path.Combine(sandbox, "server");
var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); int port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
PairingSetup.Create("127.0.0.1", configDir, port, "lock");
var pairingPath = Path.Combine(configDir, "pairing.json");
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var pairing = JsonSerializer.Deserialize<ControlPairing>(File.ReadAllText(pairingPath), json)!;
AppPathHelper.Instance.LocalityDirPath = Path.Combine(sandbox, "client");
DeviceControlClient.Import("127.0.0.1", pairingPath);
Check(DeviceControlClient.RequiresManualReconnect("127.0.0.1"), "paired Windows connection requires manual reconnect");
Check(!File.ReadAllText(Path.Combine(AppPathHelper.Instance.LocalityDirPath, ".device-control.dpapi")).Contains(pairing.Token), "Windows pairing secret encrypted with user DPAPI");
var start = new ProcessStartInfo(args.Length == 2 ? Path.GetFullPath(args[1]) : Path.Combine(repo, "artifacts", "control-review", "host", "xRemote.ControlHost.exe"))
    { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
foreach (var arg in new[] { "--console", "--read-only", "--config", Path.Combine(configDir, "server.json") }) start.ArgumentList.Add(arg);
using var host = Process.Start(start)!;
try
{
    ControlStatus? live = null;
    for (int i = 0; i < 15 && live == null; i++)
    {
        if (host.HasExited) throw new Exception("Isolated read-only host exited unexpectedly");
        try { live = await DeviceControlClient.Status("127.0.0.1"); } catch (HttpRequestException) { await Task.Delay(200); }
    }
    Check(live != null && live.Sessions.Count == native.Count, "actual pinned HTTPS client reads real Windows sessions");
    async Task<bool> Rejected(Func<Task> call) { try { await call(); return false; } catch (HttpRequestException) { return true; } catch (InvalidOperationException) { return true; } }
    var badFile = Path.Combine(sandbox, "invalid-pairing.json");
    File.WriteAllText(badFile, JsonSerializer.Serialize(pairing with { Token = new string('0', 64) }, json));
    DeviceControlClient.Import("127.0.0.1", badFile);
    Check(await Rejected(async () => { await DeviceControlClient.Status("127.0.0.1"); }), "wrong management token rejected by real HTTPS host");
    File.WriteAllText(badFile, JsonSerializer.Serialize(pairing with { CertificateSha256 = new string('0', 64) }, json));
    DeviceControlClient.Import("127.0.0.1", badFile);
    Check(await Rejected(async () => { await DeviceControlClient.Status("127.0.0.1"); }), "wrong server certificate pin rejected by real Windows client");
    DeviceControlClient.Import("127.0.0.1", pairingPath);
    Check(await Rejected(() => DeviceControlClient.Disconnect("127.0.0.1", new(Remote(9999), "test"))), "read-only host refuses mutation without touching real sessions");
}
finally { if (!host.HasExited) { host.Kill(true); await host.WaitForExitAsync(); } }
Console.WriteLine($"{passed} checks passed; no real connection disconnected; no display power changed.");

sealed class FakeBackend : ISessionBackend
{
    public List<RemoteSession> Rows = [];
    public List<int> Disconnected = [];
    public int ConsoleId => 1;
    public IReadOnlyList<RemoteSession> Read() => Rows.ToArray();
    public void Disconnect(int id) { Disconnected.Add(id); Rows = Rows.Select(s => s.Id == id ? s with { State = 4 } : s).ToList(); }
}
sealed class FakeDisplay : IDisplayPower { public int Blanks, Wakes; public void Request(bool blank) { if (blank) Blanks++; else Wakes++; } }
