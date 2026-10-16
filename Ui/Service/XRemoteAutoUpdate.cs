using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shawn.Utils;
using XRemote.Updating;

namespace _1RM.Service;

public static class XRemoteAutoUpdate
{
    private static readonly HttpClient Client = CreateClient();
    private static CancellationTokenSource _cancellation = new CancellationTokenSource();
    private static int _handedOff;
    private static bool _restart;
    private static string[] _arguments = Array.Empty<string>();
    public static bool IsReady => File.Exists(Path.Combine(State, "pending.json"));
    public static string Target => AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
    public static string State => UpdateCore.StateDirectory(Target);
    public static string LauncherPath => File.Exists(Path.Combine(Target, "xRemote.exe"))
        ? Path.Combine(Target, "xRemote.exe") : Process.GetCurrentProcess().MainModule!.FileName!;

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("xRemote/" + AppVersion.Version);
        return client;
    }

    public static void SetEnabled(bool enabled)
    {
        _cancellation.Cancel();
        if (enabled) _cancellation = new CancellationTokenSource();
        try
        {
            Directory.CreateDirectory(State);
            var disabled = Path.Combine(State, "disabled");
            if (enabled) { if (File.Exists(disabled)) File.Delete(disabled); }
            else UpdateCore.WriteAtomic(disabled, "Updates disabled by user");
        }
        catch (Exception e) { SimpleLogHelper.Warning(e); }
    }

    public static async Task<bool> PrepareAsync(string releaseUrl, Action<string> report)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token);
        timeout.CancelAfter(TimeSpan.FromMinutes(30));
        var token = timeout.Token;
        if (token.IsCancellationRequested) return false;
        Directory.CreateDirectory(State);
        UpdateCore.RejectLinks(State, State);
        // Download and installation never modify the same cache concurrently.
        using var gate = new FileStream(Path.Combine(State, "apply.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (IsReady)
        {
            var pending = UpdateCore.Verify(File.ReadAllText(Path.Combine(State, "pending.json")));
            if (System.Version.Parse(pending.Version) > CurrentVersion()) return true;
            File.Delete(Path.Combine(State, "pending.json"));
        }
        var resultPath = Path.Combine(State, "result.json");
        if (File.Exists(resultPath))
        {
            var previous = JObject.Parse(File.ReadAllText(resultPath));
            if (previous.Value<string>("state") == "failed" && previous.Value<DateTime>("at").ToUniversalTime().AddHours(6) > DateTime.UtcNow)
                throw new IOException("Update retry is deferred");
        }
        var uri = new Uri(releaseUrl);
        var prefix = "https://github.com/" + AppVersion.ReleaseRepository + "/releases/tag/";
        if (!releaseUrl.StartsWith(prefix, StringComparison.Ordinal) || uri.Scheme != "https") throw new InvalidDataException("Unexpected release source");
        var tag = Uri.UnescapeDataString(releaseUrl.Substring(prefix.Length));
        if (!System.Text.RegularExpressions.Regex.IsMatch(tag, @"^v\d+\.\d+\.\d+-r\d+$")) throw new InvalidDataException("Invalid release tag");
        var downloadBase = "https://github.com/" + AppVersion.ReleaseRepository + "/releases/download/" + tag + "/";
        using var manifestResponse = await Client.GetAsync(downloadBase + UpdateCore.ManifestAsset, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        manifestResponse.EnsureSuccessStatusCode();
        if (manifestResponse.Content.Headers.ContentLength > 4 * 1024 * 1024) throw new InvalidDataException("Manifest too large");
        string envelope;
        using (var source = await manifestResponse.Content.ReadAsStreamAsync().ConfigureAwait(false))
        using (var data = new MemoryStream())
        {
            await CopyBoundedAsync(source, data, 4 * 1024 * 1024, token).ConfigureAwait(false);
            envelope = System.Text.Encoding.UTF8.GetString(data.ToArray());
        }
        var manifest = UpdateCore.Verify(envelope);
        if (manifest.Tag != tag || System.Version.Parse(manifest.Version) <= CurrentVersion()) throw new InvalidDataException("Update version mismatch");
        report("downloading");
        var packagePath = Path.Combine(State, manifest.Package);
        if (!File.Exists(packagePath) || new FileInfo(packagePath).Length != manifest.Size || UpdateCore.HashFile(packagePath) != manifest.Sha256)
        {
            using var response = await Client.GetAsync(downloadBase + manifest.Package, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value != manifest.Size)
                throw new InvalidDataException("Download length mismatch");
            var partial = packagePath + ".partial";
            using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                await CopyBoundedAsync(input, output, manifest.Size, token).ConfigureAwait(false);
            if (new FileInfo(partial).Length != manifest.Size || UpdateCore.HashFile(partial) != manifest.Sha256) throw new InvalidDataException("Update checksum mismatch");
            if (File.Exists(packagePath)) File.Delete(packagePath);
            File.Move(partial, packagePath);
        }
        report("verifying");
        UpdateCore.ExtractVerified(packagePath, Path.Combine(State, "stage-" + manifest.Tag), manifest);
        CacheHelper();
        token.ThrowIfCancellationRequested();
        UpdateCore.WriteAtomic(Path.Combine(State, "pending.json"), envelope);
        return true;
    }

    private static async Task CopyBoundedAsync(Stream input, Stream output, long limit, CancellationToken token)
    {
        var buffer = new byte[81920];
        long count = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
        {
            count += read;
            if (count > limit) throw new InvalidDataException("Download exceeds declared size");
            await output.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
        }
    }

    private static System.Version CurrentVersion() => new System.Version((int)AppVersion.Major, (int)AppVersion.Minor, (int)AppVersion.Patch, (int)AppVersion.ForkRevision);

    private static string CacheHelper()
    {
        Directory.CreateDirectory(State);
        var source = Path.Combine(Target, "updater", "xRemote.Updater.exe");
        var hash = UpdateCore.HashFile(source);
        var destination = Path.Combine(State, "helper-" + hash + ".exe");
        if (!File.Exists(destination))
        {
            var temp = destination + ".new";
            File.Copy(source, temp, true);
            File.Move(temp, destination);
        }
        if (UpdateCore.HashFile(destination) != hash) throw new InvalidDataException("Update helper mismatch");
        UpdateCore.WriteAtomic(Path.Combine(State, "helper-name.txt"), Path.GetFileName(destination));
        return destination;
    }

    public static void RequestRestart() => _restart = true;

    // Called before profile initialization; the current stub must exit before files can be replaced.
    public static bool ContinuePendingAtStartup(string[] args)
    {
        _arguments = args;
        if (File.Exists(Path.Combine(State, "disabled")) && !File.Exists(Path.Combine(State, "transaction.json"))) return false;
        if (!IsReady && !File.Exists(Path.Combine(State, "transaction.json"))) return false;
        foreach (var name in new[] { "xRemote", "xRemote.App", "1Remote" })
        foreach (var process in Process.GetProcessesByName(name))
        {
            using (process)
            {
                if (process.Id == Process.GetCurrentProcess().Id) continue;
                try
                {
                    if (UpdateCore.Canonical(Path.GetDirectoryName(process.MainModule!.FileName)!).Equals(Target, StringComparison.OrdinalIgnoreCase)) return false;
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { return false; }
            }
        }
        return Handoff(true);
    }

    public static void OnExit(bool enabled)
    {
        _cancellation.Cancel();
        if ((enabled || _restart) && IsReady) Handoff(_restart);
    }

    private static bool Handoff(bool restart)
    {
        if (Interlocked.Exchange(ref _handedOff, 1) != 0) return true;
        try
        {
            var helper = CacheHelper();
            using var current = Process.GetCurrentProcess();
            var request = new UpdateRequest { Target = Target, ParentPid = current.Id, ParentStarted = current.StartTime.ToUniversalTime().Ticks, Restart = restart, Arguments = _arguments };
            var path = Path.Combine(State, "launch-" + Guid.NewGuid().ToString("N") + ".json");
            UpdateCore.WriteAtomic(path, JsonConvert.SerializeObject(request));
            Process.Start(new ProcessStartInfo(helper, "\"" + path + "\"") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = State });
            return true;
        }
        catch (Exception e)
        {
            Interlocked.Exchange(ref _handedOff, 0);
            SimpleLogHelper.Warning(e);
            // A pending rollback cannot safely fall through to normal startup.
            return File.Exists(Path.Combine(State, "transaction.json"));
        }
    }
}
