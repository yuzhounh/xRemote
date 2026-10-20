using System;
using System.Collections.Generic;
using System.Linq;

namespace XRemote.Control;

public sealed record RemoteSoftwareState(string Id, string Name, string State, string Evidence);
public sealed record RemoteAccessStatus(int Schema, DateTimeOffset ObservedAt,
    IReadOnlyList<RemoteSoftwareState> Software);
public sealed record RemoteAccessDecision(bool Block, string[] Active, string[] Unknown);

public static class RemoteAccessPolicy
{
    // Unknown deliberately remains usable. It is never evidence that the desktop is free.
    public static RemoteAccessDecision Evaluate(RemoteAccessStatus? status, DateTimeOffset now)
    {
        if (status?.Schema != 1 || status.Software == null || status.Software.Count == 0
            || status.Software.Any(s => s == null || string.IsNullOrWhiteSpace(s.Name))
            || now - status.ObservedAt > TimeSpan.FromSeconds(10)
            || status.ObservedAt - now > TimeSpan.FromSeconds(5))
            return new(false, Array.Empty<string>(), new[] { "status-unavailable" });

        var active = status.Software.Where(s => s.State == "active" && s.Evidence == "live")
            .Select(s => s.Name).Distinct().ToArray();
        var unknown = status.Software.Where(s => s.State != "not-running" && !(s.State == "idle" && s.Evidence == "live")
                && !(s.State == "active" && s.Evidence == "live"))
            .Select(s => s.Name).Distinct().ToArray();
        return new(active.Length > 0, active, unknown);
    }
}
