using XRemote.Control;
using XRemote.ControlHost;
using _1RM.Service;

int passed = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
RemoteAccessStatus State(string state, string evidence = "live") => new(1, DateTimeOffset.UtcNow,
    [new("uu", "UU", state, evidence)]);
RemoteAccessDecision Decide(RemoteAccessStatus? state) => RemoteAccessPolicy.Evaluate(state, DateTimeOffset.UtcNow);
Check(Decide(State("active")).Block, "confirmed incoming control blocks the desktop connection");
Check(!Decide(State("unknown")).Block && Decide(State("unknown")).Unknown.Length == 1,
    "user-selected unknown policy retains the connection with a warning");
Check(!Decide(State("active", "process-only")).Block, "a running process cannot prove control");
Check(Decide(State("idle", "process-only")).Unknown.Length == 1, "process metadata cannot prove an idle desktop");
Check(!Decide(State("active") with { ObservedAt = DateTimeOffset.UtcNow.AddSeconds(-20) }).Block,
    "past active state cannot disconnect a current connection");
Check(!Decide(State("active") with { Schema = 99 }).Block && Decide(null).Unknown.Length == 1,
    "legacy, unavailable and incompatible endpoints remain unknown");
Check(!Decide(State("active") with { Software = [null!] }).Block, "malformed provider rows remain unknown");

var names = new HashSet<string>(["GameViewerService", "ToDesk", "AweSun", "rustdesk", "TeamViewer_Service"], StringComparer.OrdinalIgnoreCase);
var inventory = RemoteSoftwareProbe.Snapshot(names, true, [], DateTimeOffset.UtcNow);
Check(inventory.Software.Count == 5 && inventory.Software.All(p => p.State == "unknown"),
    "all five background applications remain unknown without live activity adapters");
Check(RemoteSoftwareProbe.Snapshot(new HashSet<string>(), false, [], DateTimeOffset.UtcNow).Software.All(p => p.State == "unknown"),
    "an incomplete process inventory never asserts absence");
Check(RemoteSoftwareProbe.Snapshot(new HashSet<string>(), true, [], DateTimeOffset.UtcNow).Software.All(p => p.State == "not-running"),
    "complete inventory can identify absent known processes");
var verified = RemoteSoftwareProbe.Snapshot(names, true, [new Proof()], DateTimeOffset.UtcNow);
Check(Decide(verified).Block && Decide(verified).Active.SequenceEqual(["UU"]),
    "live proof can block while other providers remain unknown");

int connects = 0, stops = 0, warnings = 0;
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
await RemoteAccessMonitor.Run(_ => Task.FromResult<RemoteAccessStatus?>(State("active")), _ => { },
    () => connects++, _ => stops++, timeout.Token);
Check(connects == 0 && stops == 1, "preflight active state prevents opening the RDP transport");

connects = stops = warnings = 0;
var sequence = new Queue<RemoteAccessStatus?>([null, State("idle"), State("active")]);
await RemoteAccessMonitor.Run(_ => Task.FromResult(sequence.Dequeue()),
    d => { if (d.Unknown.Length > 0) warnings++; }, () => connects++, _ => stops++, timeout.Token, _ => Task.CompletedTask);
Check(connects == 1 && stops == 1 && warnings == 1 && sequence.Count == 0,
    "unknown connects once; later live takeover stops once and never reconnects");

connects = stops = warnings = 0;
int reads = 0;
await RemoteAccessMonitor.Run(_ => ++reads == 1 ? Task.FromException<RemoteAccessStatus?>(new IOException())
        : Task.FromResult<RemoteAccessStatus?>(State("active")),
    d => { if (d.Unknown.Length > 0) warnings++; }, () => connects++, _ => stops++, timeout.Token, _ => Task.CompletedTask);
Check(connects == 1 && warnings == 1 && stops == 1, "status failure warns without suppressing later takeover detection");

connects = stops = 0;
using var canceled = new CancellationTokenSource();
var pending = new TaskCompletionSource<RemoteAccessStatus?>(TaskCreationOptions.RunContinuationsAsynchronously);
var late = RemoteAccessMonitor.Run(_ => pending.Task, _ => { }, () => connects++, _ => stops++, canceled.Token);
canceled.Cancel(); pending.SetResult(State("active"));
try { await late; } catch (OperationCanceledException) { }
Check(connects == 0 && stops == 0, "closing the tab rejects a late status result without reviving the connection");
Console.WriteLine($"{passed} checks passed. No live remote connections, display or services were changed.");

sealed class Proof : IRemoteSoftwareActivitySource
{
    public RemoteSoftwareState? Read(string providerId) => providerId == "uu" ? new("uu", "UU", "active", "live") : null;
}
