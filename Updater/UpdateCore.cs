using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace XRemote.Updating;

public sealed class PackageFile
{
    public string Path { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Size { get; set; }
}

public sealed class UpdateRequest
{
    public string Target { get; set; } = "";
    public int ParentPid { get; set; }
    public long ParentStarted { get; set; }
    public bool Restart { get; set; }
    public string[] Arguments { get; set; } = Array.Empty<string>();
}

public sealed class UpdateManifest
{
    public int Schema { get; set; }
    public string Version { get; set; } = "";
    public string Tag { get; set; } = "";
    public string Package { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Size { get; set; }
    public List<PackageFile> Files { get; set; } = new List<PackageFile>();
}

public sealed class SignedUpdate
{
    public string Payload { get; set; } = "";
    public string Signature { get; set; } = "";
}

public sealed class BackupFile
{
    public string Path { get; set; } = "";
    public bool Existed { get; set; }
}

public sealed class UpdateTransaction
{
    public bool Committed { get; set; }
    public List<BackupFile> Files { get; set; } = new List<BackupFile>();
}

// Shared by the application, the isolated updater, and release verification tests.
public static class UpdateCore
{
    public const string ManifestAsset = "xRemote-update-win-x64.json";
    public const long MaxPackageBytes = 1024L * 1024 * 1024;
    public static string Canonical(string path) => System.IO.Path.GetFullPath(path).TrimEnd('\\', '/');
    public static string Hash(byte[] data)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
    }
    public static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    public static string StateDirectory(string target) => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "xRemote", "Updates",
        Hash(Encoding.UTF8.GetBytes(Canonical(target).ToUpperInvariant())).Substring(0, 24));

    public static string PublicKey()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("XRemote.UpdatePublicKey")
            ?? throw new InvalidDataException("Missing update verification key");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static UpdateManifest Verify(string envelope, string? publicKey = null)
    {
        if (envelope.Length > 4 * 1024 * 1024) throw new InvalidDataException("Manifest too large");
        var signed = JsonConvert.DeserializeObject<SignedUpdate>(envelope) ?? throw new InvalidDataException("Empty signature");
        var bytes = Convert.FromBase64String(signed.Payload);
        using var rsa = RSA.Create();
        rsa.FromXmlString(publicKey ?? PublicKey());
        if (!rsa.VerifyData(bytes, Convert.FromBase64String(signed.Signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            throw new InvalidDataException("Invalid update signature");
        var manifest = JsonConvert.DeserializeObject<UpdateManifest>(Encoding.UTF8.GetString(bytes))
            ?? throw new InvalidDataException("Empty update manifest");
        Validate(manifest);
        return manifest;
    }

    public static void Validate(UpdateManifest m)
    {
        var match = Regex.Match(m.Tag, @"^v(\d+)\.(\d+)\.(\d+)-r(\d+)$");
        if (m.Schema != 1 || !match.Success || !System.Version.TryParse(m.Version, out var version) ||
            version.ToString() != string.Join(".", match.Groups.Cast<Group>().Skip(1).Select(g => g.Value)) ||
            m.Package != "xRemote-" + m.Tag + "-x64.zip" || m.Size <= 0 || m.Size > MaxPackageBytes ||
            !Regex.IsMatch(m.Sha256, "^[a-f0-9]{64}$") || m.Files == null || m.Files.Count == 0 || m.Files.Count > 10000)
            throw new InvalidDataException("Invalid update identity");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var f in m.Files)
        {
            ValidateProgramPath(f.Path);
            if (!names.Add(f.Path) || f.Size < 0 || !Regex.IsMatch(f.Sha256, "^[a-f0-9]{64}$"))
                throw new InvalidDataException("Invalid update file");
            total = checked(total + f.Size);
        }
        if (total > 3 * MaxPackageBytes || !names.Contains("xRemote.exe") || !names.Contains("xRemote.App.exe") || !names.Contains("1Remote.dll") ||
            !names.Contains("updater/xRemote.Updater.exe")) throw new InvalidDataException("Incomplete update");
    }

    public static void ValidateProgramPath(string path)
    {
        var parts = path.Split('/');
        if (path.Length == 0 || path.Contains('\\') || path.Contains(':') || parts.Any(p => p.Length == 0 ||
            p == "." || p == ".." || p.StartsWith(".") || p.EndsWith(".") || p.EndsWith(" ") ||
            p.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)) throw new InvalidDataException("Unsafe update path");
        var first = parts[0];
        var name = parts[parts.Length - 1];
        if (new[] { "data", "Protocols", "KiTTY", "PuTTY", "FORCE_INTO_PORTABLE_MODE", "FORCE_INTO_APPDATA_MODE" }
                .Contains(first, StringComparer.OrdinalIgnoreCase) ||
            name.StartsWith("1Remote.json", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("1Remote.db", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("1Remote.dataSources.json", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(name, @"\.(db|sqlite|sqlite3)(-wal|-shm)?$", RegexOptions.IgnoreCase) ||
            name.StartsWith("unins", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Update would overwrite user/installation data");
    }

    public static string SafePath(string root, string relative)
    {
        ValidateProgramPath(relative);
        var result = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, relative.Replace('/', System.IO.Path.DirectorySeparatorChar)));
        if (!result.StartsWith(Canonical(root) + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Path escapes root");
        RejectLinks(root, result);
        return result;
    }

    public static void RejectLinks(string root, string path)
    {
        var current = Canonical(path);
        var stop = Canonical(root);
        while (current.Length >= stop.Length)
        {
            if ((Directory.Exists(current) || File.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Update paths must not contain links");
            if (current.Equals(stop, StringComparison.OrdinalIgnoreCase)) break;
            current = System.IO.Path.GetDirectoryName(current) ?? "";
        }
    }

    public static void WriteAtomic(string path, string content)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var temp = path + ".new";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }
        if (File.Exists(path)) File.Replace(temp, path, null);
        else File.Move(temp, path);
    }

    public static void ExtractVerified(string zip, string stage, UpdateManifest m)
    {
        if (new FileInfo(zip).Length != m.Size || HashFile(zip) != m.Sha256) throw new InvalidDataException("Package hash mismatch");
        Directory.CreateDirectory(stage);
        var expected = m.Files.ToDictionary(f => f.Path, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var archive = ZipFile.OpenRead(zip);
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith("/")) continue;
            // The same full package serves installed and portable users, preserving the existing mode.
            if (entry.FullName == "FORCE_INTO_PORTABLE_MODE") continue;
            if (!expected.TryGetValue(entry.FullName, out var f) || !seen.Add(entry.FullName) || entry.Length != f.Size)
                throw new InvalidDataException("Unexpected archive entry");
            var path = SafePath(stage, f.Path);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            using (var input = entry.Open())
            using (var output = File.Create(path)) input.CopyTo(output);
            if (HashFile(path) != f.Sha256) throw new InvalidDataException("Component hash mismatch");
        }
        if (seen.Count != expected.Count) throw new InvalidDataException("Missing components");
    }

    // The journal is durable before changing any target files. Recovery restores the whole old set.
    public static void Recover(string target, string state)
    {
        var journal = System.IO.Path.Combine(state, "transaction.json");
        if (!File.Exists(journal)) return;
        var transaction = JsonConvert.DeserializeObject<UpdateTransaction>(File.ReadAllText(journal))
            ?? throw new InvalidDataException("Invalid update journal");
        if (!transaction.Committed)
        {
            foreach (var f in transaction.Files)
            {
                var destination = SafePath(target, f.Path);
                if (f.Existed) ReplaceFrom(SafePath(System.IO.Path.Combine(state, "rollback"), f.Path), destination);
                else if (File.Exists(destination)) File.Delete(destination);
            }
        }
        else if (File.Exists(System.IO.Path.Combine(state, "pending.json"))) File.Delete(System.IO.Path.Combine(state, "pending.json"));
        File.Delete(journal);
    }

    public static void Apply(string target, string state, string? publicKey = null)
    {
        Recover(target, state);
        var pending = System.IO.Path.Combine(state, "pending.json");
        if (!File.Exists(pending)) return;
        var m = Verify(File.ReadAllText(pending), publicKey);
        var installed = System.IO.Path.Combine(target, "xRemote.exe");
        var info = FileVersionInfo.GetVersionInfo(installed);
        if (System.Version.TryParse(info.FileVersion, out var current) && System.Version.Parse(m.Version) <= current)
        {
            File.Delete(pending);
            return;
        }
        var stage = System.IO.Path.Combine(state, "stage-" + m.Tag);
        ExtractVerified(System.IO.Path.Combine(state, m.Package), stage, m);
        var rollback = System.IO.Path.Combine(state, "rollback");
        var transaction = new UpdateTransaction();
        // Copy the complete old set before beginning any replacements.
        foreach (var f in m.Files)
        {
            var destination = SafePath(target, f.Path);
            var existed = File.Exists(destination);
            if (existed)
            {
                var backup = SafePath(rollback, f.Path);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(backup)!);
                File.Copy(destination, backup, true);
            }
            transaction.Files.Add(new BackupFile { Path = f.Path, Existed = existed });
        }
        var journal = System.IO.Path.Combine(state, "transaction.json");
        WriteAtomic(journal, JsonConvert.SerializeObject(transaction));
        try
        {
            foreach (var f in m.Files)
            {
                var destination = SafePath(target, f.Path);
                ReplaceFrom(SafePath(stage, f.Path), destination);
                if (HashFile(destination) != f.Sha256) throw new IOException("Installed component differs");
            }
            transaction.Committed = true;
            WriteAtomic(journal, JsonConvert.SerializeObject(transaction));
            File.Delete(pending);
            File.Delete(journal);
            WriteAtomic(System.IO.Path.Combine(state, "result.json"), JsonConvert.SerializeObject(new { state = "applied", version = m.Version, at = DateTime.UtcNow }));
        }
        catch
        {
            Recover(target, state);
            throw;
        }
    }

    private static void ReplaceFrom(string source, string destination)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
        var temporary = destination + ".xremote-update-new";
        File.Copy(source, temporary, true);
        if (File.Exists(destination)) File.Replace(temporary, destination, null);
        else File.Move(temporary, destination);
    }
}
