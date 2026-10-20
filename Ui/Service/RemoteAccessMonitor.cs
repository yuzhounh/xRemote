using System;
using System.Threading;
using System.Threading.Tasks;
using XRemote.Control;

namespace _1RM.Service;

public static class RemoteAccessMonitor
{
    // One invocation owns one connection attempt. It never creates another connection after blocking.
    public static async Task Run(Func<CancellationToken, Task<RemoteAccessStatus?>> read,
        Action<RemoteAccessDecision> report, Action connect, Action<RemoteAccessDecision> stop,
        CancellationToken cancellation, Func<CancellationToken, Task>? wait = null)
    {
        wait ??= token => Task.Delay(TimeSpan.FromSeconds(2), token);
        bool connected = false;
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            RemoteAccessStatus? state;
            try { state = await read(cancellation).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
            catch (Exception) { state = null; }
            cancellation.ThrowIfCancellationRequested();
            var decision = RemoteAccessPolicy.Evaluate(state, DateTimeOffset.UtcNow);
            report(decision);
            cancellation.ThrowIfCancellationRequested();
            if (decision.Block) { stop(decision); return; }
            if (!connected) { connect(); connected = true; }
            await wait(cancellation).ConfigureAwait(false);
        }
    }
}
