using System.Diagnostics;
using XRemote.Control;

namespace XRemote.ControlHost;

public interface IRemoteSoftwareActivitySource
{
    // A source must prove an incoming, authenticated, currently active desktop session.
    // Outgoing connections, past sessions and a background process are not live evidence.
    RemoteSoftwareState? Read(string providerId);
}

public sealed class RemoteSoftwareProbe
{
    private static readonly (string Id, string Name, string[] Processes)[] Catalog =
    [
        ("uu", "UU", ["GameViewer", "GameViewerServer", "GameViewerService"]),
        ("todesk", "ToDesk", ["ToDesk", "ToDesk_Service", "ToDesk_Session", "ToDesk_Agent"]),
        ("sunlogin", "Sunlogin", ["SunloginClient", "SunloginClient_Desktop", "SunloginRemote", "AweSun", "OldAweSun"]),
        ("rustdesk", "RustDesk", ["rustdesk", "rustdesk-host"]),
        ("teamviewer", "TeamViewer", ["TeamViewer", "TeamViewer_Service", "TeamViewer_Desktop", "TeamViewerQS", "TeamViewer_Host"])
    ];
    private readonly object gate = new();
    private readonly IReadOnlyList<IRemoteSoftwareActivitySource> sources;
    private RemoteAccessStatus? cached;

    public RemoteSoftwareProbe(params IRemoteSoftwareActivitySource[] sources) => this.sources = sources;

    public RemoteAccessStatus Read()
    {
        lock (gate)
        {
            if (cached != null && DateTimeOffset.UtcNow - cached.ObservedAt < TimeSpan.FromSeconds(2)) return cached;
            HashSet<string> processes = new(StringComparer.OrdinalIgnoreCase);
            bool complete = true;
            try
            {
                foreach (var process in Process.GetProcesses())
                    using (process)
                        try { processes.Add(process.ProcessName); }
                        catch (InvalidOperationException) { } // A process that exited cannot host a connection.
                        catch (System.ComponentModel.Win32Exception) { complete = false; }
            }
            catch (System.ComponentModel.Win32Exception) { complete = false; }
            cached = Snapshot(processes, complete, sources, DateTimeOffset.UtcNow);
            return cached;
        }
    }

    public static RemoteAccessStatus Snapshot(ISet<string> processes, bool complete,
        IReadOnlyList<IRemoteSoftwareActivitySource> sources, DateTimeOffset now)
    {
        var rows = new List<RemoteSoftwareState>();
        foreach (var provider in Catalog)
        {
            RemoteSoftwareState? verified = null;
            foreach (var source in sources)
            {
                try
                {
                    var candidate = source.Read(provider.Id);
                    if (candidate?.Id != provider.Id || candidate.Evidence != "live"
                        || candidate.State is not ("active" or "idle")) continue;
                    verified = candidate with { Name = provider.Name };
                    if (candidate.State == "active") break;
                }
                catch (Exception) { /* A failed adapter remains unknown and cannot disconnect anyone. */ }
            }
            rows.Add(verified ?? new(provider.Id, provider.Name,
                complete && !provider.Processes.Any(processes.Contains) ? "not-running" : "unknown",
                "process-only"));
        }
        return new(1, now, rows);
    }
}
