using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace _1RM.Service;

public sealed class DeviceHardwareInfo
{
    public int Schema { get; set; }
    public string Machine { get; set; } = "";
    public DateTimeOffset ObservedAt { get; set; }
    public DeviceWindowsInfo? Windows { get; set; }
    public DeviceCpuInfo? Cpu { get; set; }
    public long? MemoryBytes { get; set; }
    public string[] Gpus { get; set; } = Array.Empty<string>();
    public DeviceDiskInfo[] Disks { get; set; } = Array.Empty<DeviceDiskInfo>();
    public DeviceVolumeInfo[] Volumes { get; set; } = Array.Empty<DeviceVolumeInfo>();
    public string[] Unavailable { get; set; } = Array.Empty<string>();
}
public sealed class DeviceWindowsInfo
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string DisplayVersion { get; set; } = "";
    public string Build { get; set; } = "";
    public string Architecture { get; set; } = "";
    public DateTimeOffset? LastBootAt { get; set; }
}
public sealed class DeviceCpuInfo
{
    public string[] Models { get; set; } = Array.Empty<string>();
    public int Sockets { get; set; }
    public int Cores { get; set; }
    public int Threads { get; set; }
}
public sealed class DeviceDiskInfo
{
    public int Index { get; set; }
    public string Model { get; set; } = "";
    public long? SizeBytes { get; set; }
}
public sealed class DeviceVolumeInfo
{
    public string Drive { get; set; } = "";
    public string Label { get; set; } = "";
    public string FileSystem { get; set; } = "";
    public long? SizeBytes { get; set; }
    public long? FreeBytes { get; set; }
    public int[] DiskNumbers { get; set; } = Array.Empty<int>();
}
public sealed class DeviceInformationException(string resourceKey) : Exception(resourceKey)
{
    public string ResourceKey { get; } = resourceKey;
}

internal static class DeviceInformationCache
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal static string PreviewPath(string address) => Path.Combine(AppPathHelper.Instance.LocalityDirPath,
        "device-information", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(address))) + ".png");
    private static string? CachePath(string address, bool storage)
    {
        var pairing = DeviceControlClient.Find(address);
        if (pairing == null) return null;
        // Bind cached inventory to the authenticated device, including after re-pairing.
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(address + "|" + pairing.CertificateSha256)));
        return Path.Combine(AppPathHelper.Instance.LocalityDirPath, "device-information", key + (storage ? ".storage.json" : ".hardware.json"));
    }
    internal static DeviceHardwareInfo? Load(string address, bool storage)
    {
        try
        {
            var path = CachePath(address, storage);
            if (path == null || !File.Exists(path) || new FileInfo(path).Length > 1024 * 1024) return null;
            var value = JsonSerializer.Deserialize<DeviceHardwareInfo>(File.ReadAllText(path), Json);
            return value?.Schema == 1 && !string.IsNullOrWhiteSpace(value.Machine) ? value : null;
        }
        catch { return null; }
    }
    internal static async Task<DeviceHardwareInfo> Read(string address, bool storage, bool refresh, CancellationToken token)
    {
        if (!refresh && Load(address, storage) is { } cached && DateTimeOffset.UtcNow - cached.ObservedAt < TimeSpan.FromHours(24))
            return cached;
        var info = await DeviceControlClient.Hardware(address, storage, refresh, token).ConfigureAwait(false);
        try
        {
            var path = CachePath(address, storage);
            if (path != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(info, Json), token).ConfigureAwait(false);
                    File.Move(temporary, path, true);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return info;
    }
}
