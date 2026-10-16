using System;
using System.Collections.Generic;

namespace XRemote.Control;

public sealed record RemoteSession(int Id, string User, string Client, int Protocol, int State,
    bool? Locked, long LogonTime, long ConnectTime)
{
    public bool IsRemoteActive => Protocol == 2 && State is 0 or 1 or 3;
    public string Identity => $"{Id}|{User}|{Client}|{Protocol}|{LogonTime}|{ConnectTime}";
}

public sealed record SessionView(RemoteSession Session, string? Ticket);
public sealed record ControlStatus(string Machine, DateTimeOffset ObservedAt, int ConsoleId,
    string PrivacyMode, string DisplayStatus, IReadOnlyList<SessionView> Sessions);
public sealed record DisconnectRequest(int SessionId, string Ticket);
public sealed record DisconnectResult(bool Disconnected, string Message);
public sealed record ControlPairing(int Schema, string Address, int Port, string CertificateSha256, string Token);
