using System.ComponentModel;
using System.Runtime.InteropServices;
using XRemote.Control;

namespace XRemote.ControlHost;

public sealed class WindowsSessions : ISessionBackend
{
    [StructLayout(LayoutKind.Sequential)] private struct SessionInfo { public int Id; public IntPtr Name; public int State; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct InfoLevel1
    {
        public int Id, State, Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)] public string Station;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 21)] public string User;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 18)] public string Domain;
        public long Logon, Connect, Disconnect, LastInput, Current;
        public uint IncomingBytes, OutgoingBytes, IncomingFrames, OutgoingFrames, IncomingCompressed, OutgoingCompressed;
    }
    [StructLayout(LayoutKind.Sequential)] private struct InfoEx { public int Level; public InfoLevel1 Data; }
    [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool WTSEnumerateSessionsW(IntPtr server, int reserved, int version, out IntPtr buffer, out int count);
    [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool WTSQuerySessionInformationW(IntPtr server, int id, int kind, out IntPtr buffer, out int bytes);
    [DllImport("wtsapi32.dll")] private static extern void WTSFreeMemory(IntPtr buffer);
    [DllImport("wtsapi32.dll", SetLastError = true)] private static extern bool WTSDisconnectSession(IntPtr server, int id, bool wait);
    [DllImport("kernel32.dll")] private static extern uint WTSGetActiveConsoleSessionId();
    public int ConsoleId => unchecked((int)WTSGetActiveConsoleSessionId());

    public IReadOnlyList<RemoteSession> Read()
    {
        if (!WTSEnumerateSessionsW(IntPtr.Zero, 0, 1, out var buffer, out int count)) throw new Win32Exception();
        try
        {
            var result = new List<RemoteSession>();
            for (int i = 0; i < count; i++)
            {
                var row = Marshal.PtrToStructure<SessionInfo>(buffer + i * Marshal.SizeOf<SessionInfo>());
                var user = Text(row.Id, 5); if (string.IsNullOrEmpty(user) && row.Id != ConsoleId) continue;
                var domain = Text(row.Id, 7); var client = Text(row.Id, 10);
                int protocol = Query(row.Id, 16, p => Marshal.ReadInt16(p));
                var info = Query(row.Id, 25, p => Marshal.PtrToStructure<InfoEx>(p));
                // Fail closed when identity timestamps cannot be read: no actionable partial snapshot.
                if (info.Level != 1 || info.Data.Id != row.Id || (info.Data.Logon <= 0 && protocol != 0))
                    throw new InvalidOperationException("无法完整读取 Windows 会话身份");
                bool? locked = info.Data.Flags == 0 ? true : info.Data.Flags == 1 ? false : null;
                result.Add(new(row.Id, string.IsNullOrEmpty(user) ? "" : domain + "\\" + user, client, protocol, row.State, locked, info.Data.Logon, info.Data.Connect));
            }
            return result;
        }
        finally { WTSFreeMemory(buffer); }
    }
    private static T Query<T>(int id, int kind, Func<IntPtr, T> read)
    {
        if (!WTSQuerySessionInformationW(IntPtr.Zero, id, kind, out var data, out _)) throw new Win32Exception();
        try { return read(data); } finally { WTSFreeMemory(data); }
    }
    private static string Text(int id, int kind) => Query(id, kind, p => Marshal.PtrToStringUni(p) ?? "");
    public void Disconnect(int id) { if (!WTSDisconnectSession(IntPtr.Zero, id, true)) throw new Win32Exception(); }
}
