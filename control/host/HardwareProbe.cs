using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace XRemote.ControlHost;

internal sealed class HardwareProbe(bool storageOnly = false)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private JsonElement? cached;
    private DateTimeOffset collected;

    public async Task<JsonElement> Read(bool refresh, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            if (!refresh && cached.HasValue && DateTimeOffset.UtcNow - collected < TimeSpan.FromSeconds(30)) return cached.Value;
            var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"))
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(AppContext.BaseDirectory, "Read-Hardware.ps1") }) start.ArgumentList.Add(argument);
            if (storageOnly) start.ArgumentList.Add("-StorageOnly");
            using var process = Process.Start(start) ?? throw new InvalidOperationException("无法启动硬件信息采集");
            var output = process.StandardOutput.ReadToEndAsync(token);
            var errors = process.StandardError.ReadToEndAsync(token);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(18));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } throw new TimeoutException("硬件信息读取超时，请稍后刷新"); }
            await errors;
            if (process.ExitCode != 0) throw new InvalidOperationException("硬件信息暂不可用，请稍后刷新");
            using var document = JsonDocument.Parse(await output);
            var value = document.RootElement;
            if (value.ValueKind != JsonValueKind.Object || value.GetProperty("schema").GetInt32() != 1 || value.GetProperty("machine").GetString() != Environment.MachineName) throw new InvalidOperationException("硬件信息响应无效");
            cached = value.Clone(); collected = DateTimeOffset.UtcNow;
            return cached.Value;
        }
        finally { gate.Release(); }
    }
}
