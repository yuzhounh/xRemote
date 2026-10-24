using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace _1RM.Service;

/// <summary>Shares reachability results only within one refresh; profiles and authentication remain separate.</summary>
internal sealed class EndpointProbeBatch
{
    private readonly Dictionary<(string Address, int Port), Task<(bool Online, long Latency, string ObservedAt)>> _requests = new();
    private readonly Func<string, int, CancellationToken, Task<bool>> _probe;
    private readonly CancellationToken _stop;

    internal EndpointProbeBatch(Func<string, int, CancellationToken, Task<bool>> probe, CancellationToken stop)
    {
        _probe = probe;
        _stop = stop;
    }

    internal Task<(bool Online, long Latency, string ObservedAt)> GetAsync(string address, int port)
    {
        _stop.ThrowIfCancellationRequested();
        var host = address.Trim();
        var key = (host.ToUpperInvariant(), port);
        lock (_requests)
        {
            if (_requests.TryGetValue(key, out var pending)) return pending;
            var request = MeasureAsync(host, port);
            _requests[key] = request;
            return request;
        }
    }

    private async Task<(bool Online, long Latency, string ObservedAt)> MeasureAsync(string address, int port)
    {
        var watch = Stopwatch.StartNew();
        var online = await _probe(address, port, _stop).ConfigureAwait(false);
        _stop.ThrowIfCancellationRequested();
        return (online, watch.ElapsedMilliseconds, DateTime.Now.ToString("HH:mm:ss"));
    }
}
