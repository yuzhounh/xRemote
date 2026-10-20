using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace XRemote.ControlHost;

// No input blocking, hooks, display driver, desktop switching, or credential collection.
public sealed class DisplayPower : IDisplayPower
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct Startup
    {
        public int Size; public string? Reserved, Desktop, Title;
        public int X, Y, Width, Height, XChars, YChars, Fill, Flags;
        public short Show, Reserved2Size; public IntPtr Reserved2, StdIn, StdOut, StdErr;
    }
    [StructLayout(LayoutKind.Sequential)] private struct ProcessInfo { public IntPtr Process, Thread; public int Id, ThreadId; }
    [StructLayout(LayoutKind.Sequential)] private struct Privileges { public int Count; public long Luid; public int Attributes; }
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern bool LookupPrivilegeValue(string? system, string name, out long luid);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool AdjustTokenPrivileges(IntPtr token, bool disable, ref Privileges state, int length, IntPtr previous, IntPtr returned);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool DuplicateTokenEx(IntPtr token, uint access, IntPtr attributes, int level, int type, out IntPtr duplicate);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool SetTokenInformation(IntPtr token, int kind, ref int data, int size);
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern bool CreateProcessAsUserW(IntPtr token, string app, StringBuilder command, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr env, string dir, ref Startup startup, out ProcessInfo info);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetExitCodeProcess(IntPtr handle, out uint code);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateProcess(IntPtr handle, uint code);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SendMessageTimeoutW(IntPtr hwnd, uint message, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);

    public static int RunHelper(bool blank, int expectedConsole)
    {
        if (!WindowsIdentity.GetCurrent().IsSystem || Process.GetCurrentProcess().SessionId != expectedConsole
            || new WindowsSessions().ConsoleId != expectedConsole) return 2;
        return SendMessageTimeoutW(new IntPtr(0xffff), 0x112, new IntPtr(0xf170), new IntPtr(blank ? 2 : -1), 2, 2000, out _) != IntPtr.Zero ? 0 : 3;
    }
    public void Request(bool blank)
    {
        if (!WindowsIdentity.GetCurrent().IsSystem) throw new InvalidOperationException("息屏需要已安装的本机服务；当前保留 Windows 锁屏");
        int console = new WindowsSessions().ConsoleId;
        if (console < 0) throw new InvalidOperationException("实体控制台正在切换，保留系统锁屏");
        var exe = Environment.ProcessPath!;
        if (!Path.GetFileNameWithoutExtension(exe).Equals("xRemote.ControlHost", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("息屏仅支持独立服务程序");
        if (!OpenProcessToken(Process.GetCurrentProcess().Handle, 0x2b, out var token)) throw new Win32Exception();
        IntPtr primary = IntPtr.Zero;
        try
        {
            foreach (var name in new[] { "SeTcbPrivilege", "SeAssignPrimaryTokenPrivilege", "SeIncreaseQuotaPrivilege" })
            {
                if (!LookupPrivilegeValue(null, name, out var luid)) throw new Win32Exception();
                var privileges = new Privileges { Count = 1, Luid = luid, Attributes = 2 };
                if (!AdjustTokenPrivileges(token, false, ref privileges, 0, IntPtr.Zero, IntPtr.Zero) || Marshal.GetLastWin32Error() != 0) throw new Win32Exception();
            }
            if (!DuplicateTokenEx(token, 0xF01FF, IntPtr.Zero, 2, 1, out primary)
                || !SetTokenInformation(primary, 12, ref console, 4)) throw new Win32Exception();
            var startup = new Startup { Size = Marshal.SizeOf<Startup>(), Desktop = @"winsta0\default" };
            var cmd = new StringBuilder($"\"{exe}\" --display {(blank ? "off" : "on")} {console}");
            if (!CreateProcessAsUserW(primary, exe, cmd, IntPtr.Zero, IntPtr.Zero, false, 0x08000000,
                    IntPtr.Zero, AppContext.BaseDirectory, ref startup, out var info)) throw new Win32Exception();
            try
            {
                if (WaitForSingleObject(info.Process, 4000) != 0)
                {
                    TerminateProcess(info.Process, 4); // Only this just-created, owned display helper.
                    WaitForSingleObject(info.Process, 1000);
                    throw new InvalidOperationException("显示请求超时，已停止本次助手");
                }
                if (!GetExitCodeProcess(info.Process, out uint code) || code != 0)
                    throw new InvalidOperationException("显示唤醒/息屏请求未完成，需检查实体显示器");
            }
            finally { CloseHandle(info.Process); CloseHandle(info.Thread); }
        }
        finally { if (primary != IntPtr.Zero) CloseHandle(primary); CloseHandle(token); }
    }
}
