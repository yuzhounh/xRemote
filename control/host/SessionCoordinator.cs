using System.Security.Cryptography;
using XRemote.Control;

namespace XRemote.ControlHost;

public interface ISessionBackend
{
    IReadOnlyList<RemoteSession> Read();
    int ConsoleId { get; }
    void Disconnect(int id);
}
public interface IDisplayPower { void Request(bool blank); }

public sealed class SessionCoordinator(ISessionBackend backend, IDisplayPower display, string privacyMode)
{
    private readonly object gate = new();
    private readonly Dictionary<string, (int Id, string Identity, DateTimeOffset Expires)> tickets = new();
    private HashSet<string>? previousRemote;
    private DateTimeOffset blankPendingUntil;
    public string DisplayStatus { get; private set; } = "系统锁屏";
    public Action<string>? Audit { get; set; }

    public ControlStatus Status()
    {
        lock (gate)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var key in tickets.Where(t => t.Value.Expires <= now).Select(t => t.Key).ToArray()) tickets.Remove(key);
            // Keep polling bounded even if a paired client repeatedly requests snapshots.
            if (tickets.Count > 512) tickets.Clear();
            var views = backend.Read().Select(s =>
            {
                string? ticket = null;
                if (s.IsRemoteActive)
                {
                    ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                    tickets[ticket] = (s.Id, s.Identity, now.AddSeconds(45));
                }
                return new SessionView(s, ticket);
            }).ToArray();
            return new(Environment.MachineName, now, backend.ConsoleId, privacyMode, DisplayStatus, views);
        }
    }

    public DisconnectResult Disconnect(DisconnectRequest request)
    {
        lock (gate)
        {
            if (!tickets.Remove(request.Ticket ?? "", out var issued) || issued.Expires < DateTimeOffset.UtcNow
                || issued.Id != request.SessionId) throw new InvalidOperationException("操作已过期，请刷新会话列表");
            var current = backend.Read().SingleOrDefault(s => s.Id == request.SessionId);
            if (current == null || !current.IsRemoteActive || current.Identity != issued.Identity)
                throw new InvalidOperationException("该会话已变化，请刷新后重新选择");
            backend.Disconnect(current.Id); // Disconnect only; never log off or terminate processes.
            Audit?.Invoke($"disconnect session={current.Id}");
            // Read back before claiming success; a reconnect is a new identity and a new user action.
            if (backend.Read().Any(s => s.Id == current.Id && s.IsRemoteActive && s.Identity == current.Identity))
                throw new InvalidOperationException("尚未确认会话断开，请刷新状态");
            if (!backend.Read().Any(s => s.IsRemoteActive)) Wake();
            return new(true, "连接已断开，Windows 会话与程序保留");
        }
    }

    // Called only by SCM's trusted session-unlock/logon notification, never by an HTTP client.
    public void LocalAuthenticated(int sessionId)
    {
        lock (gate)
        {
            var sessions = backend.Read();
            var local = sessions.SingleOrDefault(s => s.Id == sessionId);
            if (sessionId != backend.ConsoleId || local is not { Protocol: 0, State: 0, Locked: false }
                || string.IsNullOrWhiteSpace(local.User)) return;
            blankPendingUntil = default;
            // Server sessions belonging to other accounts must not be disconnected.
            foreach (var remote in sessions.Where(s => s.IsRemoteActive &&
                         string.Equals(s.User, local.User, StringComparison.OrdinalIgnoreCase)))
            {
                var live = backend.Read().SingleOrDefault(s => s.Id == remote.Id);
                if (live?.Identity != remote.Identity || !live.IsRemoteActive) continue;
                backend.Disconnect(live.Id);
                Audit?.Invoke($"local-reclaim console={sessionId} remote={live.Id}");
            }
            Wake();
        }
    }

    public void Observe()
    {
        lock (gate)
        {
            var sessions = backend.Read();
            var active = sessions.Where(s => s.IsRemoteActive).Select(s => s.Identity).ToHashSet();
            // Never blank the screen just because the service was installed/restarted.
            bool newConnection = previousRemote != null && active.Except(previousRemote).Any();
            if (newConnection) blankPendingUntil = DateTimeOffset.UtcNow.AddSeconds(10);
            bool hadRemote = previousRemote?.Count > 0;
            previousRemote = active;
            if (active.Count == 0) { blankPendingUntil = default; if (hadRemote || DisplayStatus != "系统锁屏") Wake(); return; }
            var console = sessions.SingleOrDefault(s => s.Id == backend.ConsoleId);
            if (privacyMode == "black" && blankPendingUntil > DateTimeOffset.UtcNow && console?.Locked == true)
            {
                blankPendingUntil = default;
                try { display.Request(true); }
                catch { DisplayStatus = "息屏不可用，保留 Windows 锁屏"; return; }
                // A power request is not proof that every physical monitor has gone black.
                DisplayStatus = "已请求息屏；本机按键可唤醒 Windows 登录界面";
            }
        }
    }
    public void Wake() { lock (gate) { display.Request(false); DisplayStatus = "系统锁屏"; } }
}
