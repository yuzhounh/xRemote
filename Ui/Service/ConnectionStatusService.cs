using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using _1RM.Model;
using _1RM.Model.Protocol.Base;
using _1RM.Utils;
using _1RM.View;
using Shawn.Utils;
using Stylet;

namespace _1RM.Service;

/// <summary>Checks reachability and optional authenticated management status; never creates an RDP session.</summary>
public sealed class ConnectionStatusService : IDisposable
{
    private readonly GlobalData _data;
    private readonly CancellationTokenSource _stop = new();
    private Timer? _timer;
    private int _refreshing;

    public ConnectionStatusService(GlobalData data) => _data = data;

    public void Start()
    {
        if (_timer != null) return;
        _data.OnReloadAll += RequestRefresh;
        _timer = new Timer(_ => RequestRefresh(), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    public void RequestRefresh()
    {
        if (!_stop.IsCancellationRequested) _ = RefreshAsync();
    }

    internal static async Task<bool> ProbeAsync(string address, int port, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        var result = await TcpHelper.TestConnectionAsync(address, port, timeout.Token, 3500).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return result == true;
    }

    private async Task RefreshAsync()
    {
        if (Interlocked.Exchange(ref _refreshing, 1) != 0) return;
        try
        {
            var token = _stop.Token;
            var rows = Array.Empty<ProtocolBaseViewModel>();
            await Execute.OnUIThreadAsync(() => rows = _data.VmItemList.ToArray());
            using var slots = new SemaphoreSlim(4);
            await Task.WhenAll(rows.Select(async row =>
            {
                if (row.Server is not ProtocolBaseWithAddressPort endpoint) return;
                var address = endpoint.Address;
                if (string.IsNullOrWhiteSpace(address) || !int.TryParse(endpoint.Port, out var port) || port is < 1 or > 65535)
                {
                    await Execute.OnUIThreadAsync(() => { row.SetRemoteSessionStatus(null); row.SetConnectionStatus(false, "Shell.MissingEndpoint"); });
                    return;
                }
                await slots.WaitAsync(token);
                try
                {
                    var watch = Stopwatch.StartNew();
                    var online = await ProbeAsync(address, port, token).ConfigureAwait(false);
                    var latency = watch.ElapsedMilliseconds;
                    var hintKey = online ? "Shell.ReachableHint" : "Shell.UnreachableHint";
                    var observedAt = DateTime.Now.ToString("HH:mm:ss");
                    XRemote.Control.ControlStatus? remote = null;
                    if (endpoint is _1RM.Model.Protocol.RDP)
                    {
                        try { if (DeviceControlClient.Find(address) != null) remote = await DeviceControlClient.Status(address, token).ConfigureAwait(false); }
                        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                        catch { /* Unavailable management is unknown, never idle. */ }
                    }
                    await Execute.OnUIThreadAsync(() =>
                    {
                        // Ignore stale results when a profile was replaced/edited during a probe.
                        if (_data.VmItemList.Contains(row) && ReferenceEquals(row.Server, endpoint)
                            && endpoint.Address == address && endpoint.Port == port.ToString())
                        {
                            row.SetRemoteSessionStatus(remote);
                            row.SetConnectionStatus(online, hintKey, address, port, latency, observedAt);
                        }
                    });
                }
                finally { slots.Release(); }
            }));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!_stop.IsCancellationRequested) SimpleLogHelper.Error(ex); }
        finally { Interlocked.Exchange(ref _refreshing, 0); }
    }

    public void Dispose()
    {
        _data.OnReloadAll -= RequestRefresh;
        _timer?.Dispose();
        _timer = null;
        _stop.Cancel();
    }
}
