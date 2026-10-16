using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XRemote.Control;

namespace _1RM.Service;

public static class DeviceControlClient
{
    private static readonly object Gate = new();
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("xRemote.device-control.v1");
    private static string StorePath => Path.Combine(AppPathHelper.Instance.LocalityDirPath, ".device-control.dpapi");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static bool RequiresManualReconnect(string address)
    {
        try { return Find(address) != null; }
        catch { return true; } // A broken local pairing store must not revive a deliberately ended session.
    }
    public static ControlPairing? Find(string address)
    {
        lock (Gate)
        {
            if (!File.Exists(StorePath)) return null;
            var bytes = ProtectedData.Unprotect(File.ReadAllBytes(StorePath), Entropy, DataProtectionScope.CurrentUser);
            try { return JsonSerializer.Deserialize<Dictionary<string, ControlPairing>>(bytes, Json)?.GetValueOrDefault(address); }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
    }
    public static void Import(string address, string path)
    {
        if (new FileInfo(path).Length > 16384) throw new InvalidOperationException(UiText.Get("Shell.PairingLarge"));
        var pairing = JsonSerializer.Deserialize<ControlPairing>(File.ReadAllText(path), Json)
            ?? throw new InvalidOperationException(UiText.Get("Shell.PairingInvalid"));
        if (pairing.Schema != 1 || pairing.Address != address || pairing.Port is < 1024 or > 65535
            || !IsHex(pairing.Token) || !IsHex(pairing.CertificateSha256))
            throw new InvalidOperationException(UiText.Get("Shell.PairingMismatch"));
        lock (Gate)
        {
            var rows = new Dictionary<string, ControlPairing>();
            if (File.Exists(StorePath))
            {
                var data = ProtectedData.Unprotect(File.ReadAllBytes(StorePath), Entropy, DataProtectionScope.CurrentUser);
                try { rows = JsonSerializer.Deserialize<Dictionary<string, ControlPairing>>(data, Json) ?? rows; }
                finally { CryptographicOperations.ZeroMemory(data); }
            }
            rows[address] = pairing;
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            var encrypted = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(rows, Json), Entropy, DataProtectionScope.CurrentUser);
            var temp = StorePath + ".new"; File.WriteAllBytes(temp, encrypted); File.Move(temp, StorePath, true);
        }
    }
    private static bool IsHex(string value) => value != null && value.Length == 64 && System.Linq.Enumerable.All(value, Uri.IsHexDigit);
    private static HttpClient Open(string address)
    {
        var pairing = Find(address) ?? throw new InvalidOperationException(UiText.Get("Shell.Unpaired"));
        var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false };
        handler.ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert != null
            && cert.NotBefore.ToUniversalTime() <= DateTime.UtcNow && cert.NotAfter.ToUniversalTime() >= DateTime.UtcNow
            && string.Equals(Convert.ToHexString(SHA256.HashData(cert.RawData)), pairing.CertificateSha256, StringComparison.OrdinalIgnoreCase);
        var client = new HttpClient(handler) { BaseAddress = new UriBuilder("https", address, pairing.Port).Uri, Timeout = TimeSpan.FromSeconds(4) };
        client.DefaultRequestHeaders.Authorization = new("Bearer", pairing.Token);
        return client;
    }
    public static async Task<ControlStatus> Status(string address, CancellationToken token = default)
    {
        using var client = Open(address);
        return await client.GetFromJsonAsync<ControlStatus>("v1/status", Json, token).ConfigureAwait(false)
            ?? throw new InvalidOperationException(UiText.Get("Shell.NoSessionStatus"));
    }
    public static async Task Disconnect(string address, SessionView selected)
    {
        using var client = Open(address);
        using var response = await client.PostAsJsonAsync("v1/disconnect", new DisconnectRequest(selected.Session.Id, selected.Ticket!), Json);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(UiText.Get("Remote.DisconnectFailed"));
        var result = await response.Content.ReadFromJsonAsync<DisconnectResult>(Json);
        if (result?.Disconnected != true) throw new InvalidOperationException(UiText.Get("Shell.DisconnectUnconfirmed"));
    }
}
