using System;
using System.Collections.Generic;
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
    private Timer? _informationTimer;
    private int _refreshing;
    private int _refreshingInformation;
    private readonly Dictionary<string, Task<XRemote.Control.ControlStatus?>> _remoteRequests = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _informationRetryAfter = new(StringComparer.OrdinalIgnoreCase);

    public ConnectionStatusService(GlobalData data) => _data = data;

    public void Start()
    {
        if (_timer != null) return;
        _data.OnReloadAll += RequestRefresh;
        _data.OnReloadAll += RequestInformationRefresh;
        _timer = new Timer(_ => RequestRefresh(), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        _informationTimer = new Timer(_ => RequestInformationRefresh(), null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    public void RequestRefresh()
    {
        if (!_stop.IsCancellationRequested) _ = RefreshAsync();
    }

    // Both the main poller and detail buttons share each in-flight remote-status request.
    private Task<XRemote.Control.ControlStatus?> ReadRemoteStatusAsync(string address)
    {
        lock (_remoteRequests)
        {
            if (_remoteRequests.TryGetValue(address, out var pending)) return pending;
            var completion = new TaskCompletionSource<XRemote.Control.ControlStatus?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _remoteRequests[address] = completion.Task;
            _ = CompleteRemoteStatusAsync(address, completion);
            return completion.Task;
        }
    }
    private async Task CompleteRemoteStatusAsync(string address, TaskCompletionSource<XRemote.Control.ControlStatus?> completion)
    {
        XRemote.Control.ControlStatus? status = null;
        try
        {
            if (DeviceControlClient.Find(address) != null)
                status = await DeviceControlClient.Status(address, _stop.Token).ConfigureAwait(false);
        }
        catch { /* An unavailable management service means unknown usage, never idle. */ }
        finally
        {
            lock (_remoteRequests) _remoteRequests.Remove(address);
            completion.TrySetResult(status);
        }
    }
    internal async Task RefreshDeviceAsync(ProtocolBaseViewModel row)
    {
        if (_stop.IsCancellationRequested || row.Server is not _1RM.Model.Protocol.RDP endpoint) return;
        var address = endpoint.Address;
        var status = await ReadRemoteStatusAsync(address).ConfigureAwait(false);
        await Execute.OnUIThreadAsync(() =>
        {
            if (_data.VmItemList.Contains(row) && ReferenceEquals(row.Server, endpoint) && endpoint.Address == address)
                row.SetRemoteSessionStatus(status);
        });
    }
    private void RequestInformationRefresh()
    {
        if (!_stop.IsCancellationRequested) _ = RefreshInformationAsync();
    }
    private async Task RefreshInformationAsync()
    {
        if (Interlocked.Exchange(ref _refreshingInformation, 1) != 0) return;
        try
        {
            var addresses = Array.Empty<string>();
            await Execute.OnUIThreadAsync(() => addresses = _data.VmItemList
                .Select(row => (row.Server as ProtocolBaseWithAddressPort)?.Address)
                .Where(address => !string.IsNullOrWhiteSpace(address)).Select(address => address!)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            // Inventory collection is separate from the 30-second status poll and uses one worker.
            foreach (var address in addresses)
            {
                if (_stop.IsCancellationRequested) return;
                if (_informationRetryAfter.TryGetValue(address, out var retry) && DateTimeOffset.UtcNow < retry) continue;
                try
                {
                    if (DeviceControlClient.Find(address) == null) continue;
                    await DeviceInformationCache.Read(address, false, false, _stop.Token).ConfigureAwait(false);
                    await DeviceInformationCache.Read(address, true, false, _stop.Token).ConfigureAwait(false);
                    _informationRetryAfter.Remove(address);
                }
                catch (OperationCanceledException) when (_stop.IsCancellationRequested) { return; }
                catch (Exception ex)
                {
                    _informationRetryAfter[address] = DateTimeOffset.UtcNow.AddMinutes(5);
                    SimpleLogHelper.Warning("Device information refresh deferred: " + ex.Message);
                }
            }
        }
        catch (Exception ex) { if (!_stop.IsCancellationRequested) SimpleLogHelper.Error(ex); }
        finally { Interlocked.Exchange(ref _refreshingInformation, 0); }
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
                        remote = await ReadRemoteStatusAsync(address).ConfigureAwait(false);
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
        _data.OnReloadAll -= RequestInformationRefresh;
        _timer?.Dispose();
        _informationTimer?.Dispose();
        _informationTimer = null;
        _timer = null;
        _stop.Cancel();
    }
}
