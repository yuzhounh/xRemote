using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using XRemote.Control;

namespace XRemote.ControlHost;

public static class PairingSetup
{
    public static void Create(string address, string directory, int port, string privacy)
    {
        if (!IPAddress.TryParse(address, out var ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any)
            || privacy is not ("black" or "lock") || port is < 1024 or > 65535) throw new ArgumentException("初始化参数无效");
        directory = Path.GetFullPath(directory); Directory.CreateDirectory(directory);
        if (Directory.EnumerateFileSystemEntries(directory).Any()) throw new InvalidOperationException("配置目录必须为空，现有配对不覆盖");
        var acl = new DirectorySecurity(); acl.SetAccessRuleProtection(true, false);
        var identities = new List<SecurityIdentifier> { new("S-1-5-18"), new("S-1-5-32-544") };
        var current = WindowsIdentity.GetCurrent();
        if (!current.IsSystem && !new WindowsPrincipal(current).IsInRole(WindowsBuiltInRole.Administrator)) identities.Add(current.User!);
        foreach (var identity in identities)
            acl.AddAccessRule(new FileSystemAccessRule(identity, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(directory).SetAccessControl(acl);
        using var rsa = RSA.Create(3072);
        var request = new CertificateRequest("CN=xRemote Control", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder(); san.AddIpAddress(ip); request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, true));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(2));
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        File.WriteAllBytes(Path.Combine(directory, "server.pfx"), certificate.Export(X509ContentType.Pfx));
        File.WriteAllText(Path.Combine(directory, "server.json"), JsonSerializer.Serialize(new HostConfig(address, port, "server.pfx", token, privacy), json));
        var pairing = new ControlPairing(1, address, port, Convert.ToHexString(SHA256.HashData(certificate.RawData)), token);
        File.WriteAllText(Path.Combine(directory, "pairing.json"), JsonSerializer.Serialize(pairing, json));
    }
}
