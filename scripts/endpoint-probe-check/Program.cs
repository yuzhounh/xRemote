using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using _1RM.Service;

static class Program
{
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    static async Task Main()
    {
        var calls = 0;
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> Probe(string host, int port, CancellationToken token)
        {
            Interlocked.Increment(ref calls);
            Check(host == host.Trim(), "Probe host must be trimmed");
            return gate.Task;
        }
        var batch = new EndpointProbeBatch(Probe, CancellationToken.None);
        var first = batch.GetAsync(" Server.Example ", 3389);
        Check(ReferenceEquals(first, batch.GetAsync("server.example", 3389)), "Same endpoint must share the pending task");
        var otherPort = batch.GetAsync("server.example", 3390);
        var otherHost = batch.GetAsync("other.example", 3389);
        Check(calls == 3, "Different hosts and ports must remain separate");
        gate.SetResult(false);
        var offline = await first;
        await Task.WhenAll(otherPort, otherHost);
        Check(!offline.Online && offline.ObservedAt.Length > 0, "Offline result and observation must be retained");
        Check(ReferenceEquals(first, batch.GetAsync("SERVER.EXAMPLE", 3389)), "Completed result must remain shared for this round");
        await new EndpointProbeBatch(Probe, CancellationToken.None).GetAsync("server.example", 3389);
        Check(calls == 4, "A new refresh must probe again");

        var active = 0;
        var peak = 0;
        var boundedCalls = 0;
        using var slots = new SemaphoreSlim(4);
        var bounded = new EndpointProbeBatch(async (_, _, token) =>
        {
            var count = Interlocked.Increment(ref active);
            int old;
            do { old = peak; } while (old < count && Interlocked.CompareExchange(ref peak, count, old) != old);
            Interlocked.Increment(ref boundedCalls);
            try { await Task.Delay(15, token); return true; }
            finally { Interlocked.Decrement(ref active); }
        }, CancellationToken.None);
        await Task.WhenAll(Enumerable.Range(0, 32).Select(async i =>
        {
            await slots.WaitAsync();
            try { await bounded.GetAsync("server" + (i % 8), 3389); }
            finally { slots.Release(); }
        }));
        Check(peak <= 4 && boundedCalls == 8, "Existing four-slot scheduling must bound probes and share duplicate rows");

        using var stop = new CancellationTokenSource();
        var cancelled = new EndpointProbeBatch(async (_, _, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return true;
        }, stop.Token);
        var pending = cancelled.GetAsync("server", 3389);
        stop.Cancel();
        try { await pending; throw new Exception("Pending probe must cancel"); }
        catch (OperationCanceledException) { }
        try { _ = cancelled.GetAsync("server", 3389); throw new Exception("Cancelled batch must reject new callers"); }
        catch (OperationCanceledException) { }
        Console.WriteLine("PASS: endpoint sharing, host/port separation, offline result, refresh lifetime, four-slot bound and cancellation");
    }
}
