namespace _1RM.Utils;

/// <summary>User/server disconnects must never trigger an application retry.</summary>
internal static class RdpReconnectPolicy
{
    internal static bool CanRetryAutomatically(int disconnectReason, int extendedReason)
    {
        // 1: local disconnect/cancel, 2: remote user, 3: server disconnect/takeover.
        // An extended server reason (API disconnect, replacement, timeout, etc.)
        // must also win when the outer reason happens to look like a transport error.
        return disconnectReason > 3 && extendedReason == 0;
    }
}
