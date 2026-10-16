using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;

namespace XRemote.Updating;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        string? state = null;
        UpdateRequest? request = null;
        string? requestFile = null;
        try
        {
            if (args.Length >= 4 && args[0] == "--resume")
                request = new UpdateRequest { Target = args[1], ParentPid = int.Parse(args[2]), ParentStarted = long.Parse(args[3]), Restart = true, Arguments = args.Skip(4).ToArray() };
            else if (args.Length == 1)
            {
                requestFile = args[0];
                request = JsonConvert.DeserializeObject<UpdateRequest>(File.ReadAllText(requestFile)) ?? throw new InvalidDataException();
            }
            else return 2;
            var target = UpdateCore.Canonical(request.Target);
            state = UpdateCore.StateDirectory(target);
            if (requestFile != null && !UpdateCore.Canonical(Path.GetDirectoryName(Path.GetFullPath(requestFile))!).Equals(state, StringComparison.OrdinalIgnoreCase)) return 2;
            UpdateCore.RejectLinks(state, state);
            try
            {
                using var parent = Process.GetProcessById(request.ParentPid);
                if (parent.StartTime.ToUniversalTime().Ticks == request.ParentStarted && !parent.WaitForExit(120000)) return 3;
            }
            catch (ArgumentException) { } // Parent has already exited.
            using var gate = AcquireGate(Path.Combine(state, "apply.lock"));
            if (HasRunningApplication(target)) return 3; // Never close or kill a running application.
            UpdateCore.Apply(target, state);
            if (request.Restart) StartApplication(request);
            if (requestFile != null) File.Delete(requestFile);
            return 0;
        }
        catch (Exception e)
        {
            if (state != null)
            {
                try
                {
                    UpdateCore.WriteAtomic(Path.Combine(state, "result.json"), JsonConvert.SerializeObject(new { state = "failed", message = e.Message, at = DateTime.UtcNow }));
                    // A recovered old version may start; an unfinished rollback must remain blocked.
                    if (!File.Exists(Path.Combine(state, "transaction.json")))
                    {
                        var pending = Path.Combine(state, "pending.json");
                        if (File.Exists(pending)) File.Move(pending, Path.Combine(state, "failed-" + Guid.NewGuid().ToString("N") + ".json"));
                        if (request?.Restart == true) StartApplication(request);
                    }
                }
                catch { }
            }
            return 1;
        }
    }

    private static FileStream AcquireGate(string path)
    {
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (true)
        {
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (DateTime.UtcNow < deadline) { Thread.Sleep(200); }
        }
    }

    public static bool HasRunningApplication(string target)
    {
        foreach (var name in new[] { "xRemote", "xRemote.App", "1Remote" })
        foreach (var process in Process.GetProcessesByName(name))
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId) continue;
                try
                {
                    if (UpdateCore.Canonical(Path.GetDirectoryName(process.MainModule!.FileName)!).Equals(target, StringComparison.OrdinalIgnoreCase)) return true;
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { return true; } // Fail closed if occupancy is unknown.
            }
        }
        return false;
    }

    private static void StartApplication(UpdateRequest request)
    {
        var start = new ProcessStartInfo(Path.Combine(request.Target, "xRemote.exe")) { WorkingDirectory = request.Target, UseShellExecute = false };
        foreach (var arg in request.Arguments) start.ArgumentList.Add(arg);
        Process.Start(start);
    }
}
