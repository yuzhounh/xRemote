using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using XRemote.Updating;

if (args.Length == 4 && args[0] == "--verify-package")
{
    var envelope = File.ReadAllText(Path.Combine(args[1], UpdateCore.ManifestAsset));
    var manifest = UpdateCore.Verify(envelope, File.ReadAllText(args[2]));
    UpdateCore.ExtractVerified(Path.Combine(args[1], manifest.Package), args[3], manifest);
    foreach (var file in new[] { "xRemote.exe", "xRemote.App.exe", "1Remote.dll" })
        if (System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(args[3], file)).FileVersion != manifest.Version)
            throw new Exception("Packaged binary version mismatch: " + file);
    Console.WriteLine("PASS final production-signed full package, every component checksum and executable version: " + manifest.Version);
    return;
}

var root = Path.GetFullPath(args.Length > 0 ? args[0] : "tmp/update-tests");
Directory.CreateDirectory(root);
using var rsa = RSA.Create(2048);
var publicKey = rsa.ToXmlString(false);
int passed = 0;
void Check(string name, Action test)
{
    test();
    Console.WriteLine("PASS " + name);
    passed++;
}
void Need(bool condition, string why) { if (!condition) throw new Exception(why); }
void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Expected rejection"); }
string Sign(UpdateManifest manifest)
{
    var data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(manifest));
    return JsonConvert.SerializeObject(new SignedUpdate { Payload = Convert.ToBase64String(data), Signature = Convert.ToBase64String(rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)) });
}
(string Target, string State, UpdateManifest Manifest, string Envelope) Fixture(string name, bool extra = false)
{
    var directory = Path.Combine(root, name + "-" + Guid.NewGuid().ToString("N"));
    var target = Path.Combine(directory, "app");
    var state = Path.Combine(directory, "state");
    Directory.CreateDirectory(target);
    Directory.CreateDirectory(state);
    File.WriteAllText(Path.Combine(target, "xRemote.exe"), "old executable");
    File.WriteAllText(Path.Combine(target, "1Remote.dll"), "old component");
    var files = new Dictionary<string, string> { ["xRemote.exe"] = "new executable", ["xRemote.App.exe"] = "new app host", ["1Remote.dll"] = "new component", ["updater/xRemote.Updater.exe"] = "new helper", ["resources/new.txt"] = "new resource" };
    var m = new UpdateManifest { Schema = 1, Version = "1.3.0.999", Tag = "v1.3.0-r999", Package = "xRemote-v1.3.0-r999-x64.zip" };
    using (var archive = ZipFile.Open(Path.Combine(state, m.Package), ZipArchiveMode.Create))
    {
        foreach (var pair in files)
        {
            var bytes = Encoding.UTF8.GetBytes(pair.Value);
            var entry = archive.CreateEntry(pair.Key);
            using var output = entry.Open();
            output.Write(bytes);
            m.Files.Add(new PackageFile { Path = pair.Key, Sha256 = UpdateCore.Hash(bytes), Size = bytes.Length });
        }
        archive.CreateEntry("FORCE_INTO_PORTABLE_MODE");
        if (extra) archive.CreateEntry("../data/config.json");
    }
    var zip = Path.Combine(state, m.Package);
    m.Size = new FileInfo(zip).Length;
    m.Sha256 = UpdateCore.HashFile(zip);
    var envelope = Sign(m);
    File.WriteAllText(Path.Combine(state, "pending.json"), envelope);
    return (target, state, m, envelope);
}

Check("valid signature and exact version/package binding", () =>
{
    var f = Fixture("signature");
    Need(UpdateCore.Verify(f.Envelope, publicKey).Version == f.Manifest.Version, "Version changed");
    var tampered = JsonConvert.DeserializeObject<SignedUpdate>(f.Envelope)!;
    tampered.Payload = Convert.ToBase64String(Encoding.UTF8.GetBytes("{}"));
    Reject(() => UpdateCore.Verify(JsonConvert.SerializeObject(tampered), publicKey));
    f.Manifest.Tag = "v1.3.0-r998";
    Reject(() => UpdateCore.Verify(Sign(f.Manifest), publicKey));
});
Check("reject traversal and every protected profile location", () =>
{
    foreach (var path in new[] { "../xRemote.exe", "a/../../data", "C:/temp/evil", "a\\b", "a/file:stream", ".locality/password.json", "Protocols/custom.exe", "data/config.json", "1Remote.json", "1Remote.db-wal", "1Remote.dataSources.json", "nested/test.sqlite", "FORCE_INTO_APPDATA_MODE", "unins000.dat" })
        Reject(() => UpdateCore.ValidateProgramPath(path));
});
foreach (var mode in new[] { "installed", "portable" })
Check(mode + ": full replacement preserves settings, data, mode and custom files", () =>
{
    var f = Fixture(mode);
    var protectedFiles = new[] { "1Remote.json", "1Remote.db", "1Remote.dataSources.json", ".locality/.rdp-passwords.json", ".icons/custom.png", "Protocols/custom.json", "KiTTY/session", "PuTTY/session", "my-custom-files.txt", mode == "portable" ? "FORCE_INTO_PORTABLE_MODE" : "FORCE_INTO_APPDATA_MODE" };
    foreach (var path in protectedFiles)
    {
        var full = Path.Combine(f.Target, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "private " + path);
    }
    var hashes = protectedFiles.ToDictionary(p => p, p => UpdateCore.HashFile(Path.Combine(f.Target, p)));
    UpdateCore.Apply(f.Target, f.State, publicKey);
    Need(File.ReadAllText(Path.Combine(f.Target, "xRemote.exe")) == "new executable", "EXE not updated");
    Need(File.ReadAllText(Path.Combine(f.Target, "1Remote.dll")) == "new component", "DLL not updated");
    foreach (var p in protectedFiles) Need(UpdateCore.HashFile(Path.Combine(f.Target, p)) == hashes[p], "Data overwritten: " + p);
    Need(!File.Exists(Path.Combine(f.State, "pending.json")) && !File.Exists(Path.Combine(f.State, "transaction.json")), "Update not committed");
    Need(!File.Exists(Path.Combine(f.Target, mode == "portable" ? "FORCE_INTO_APPDATA_MODE" : "FORCE_INTO_PORTABLE_MODE")), "Storage mode changed");
});
Check("corrupt download cannot alter installed files", () =>
{
    var f = Fixture("corrupt");
    File.AppendAllText(Path.Combine(f.State, f.Manifest.Package), "corrupt");
    Reject(() => UpdateCore.Apply(f.Target, f.State, publicKey));
    Need(File.ReadAllText(Path.Combine(f.Target, "xRemote.exe")) == "old executable", "Corrupt package applied");
});
Check("unlisted ZIP entries are rejected before installation", () =>
{
    var f = Fixture("extra", true);
    Reject(() => UpdateCore.Apply(f.Target, f.State, publicKey));
    Need(File.ReadAllText(Path.Combine(f.Target, "xRemote.exe")) == "old executable", "Partial update applied");
});
Check("interrupted transaction restores old files and removes newly added components", () =>
{
    var f = Fixture("interrupted");
    Directory.CreateDirectory(Path.Combine(f.State, "rollback"));
    File.Copy(Path.Combine(f.Target, "xRemote.exe"), Path.Combine(f.State, "rollback/xRemote.exe"));
    File.WriteAllText(Path.Combine(f.Target, "xRemote.exe"), "half applied");
    File.WriteAllText(Path.Combine(f.Target, "new.dll"), "partial new component");
    UpdateCore.WriteAtomic(Path.Combine(f.State, "transaction.json"), JsonConvert.SerializeObject(new UpdateTransaction { Files = new List<BackupFile> { new() { Path = "xRemote.exe", Existed = true }, new() { Path = "new.dll", Existed = false } } }));
    UpdateCore.Recover(f.Target, f.State);
    Need(File.ReadAllText(Path.Combine(f.Target, "xRemote.exe")) == "old executable", "Rollback failed");
    Need(!File.Exists(Path.Combine(f.Target, "new.dll")), "Partial added file retained");
    UpdateCore.Apply(f.Target, f.State, publicKey);
    Need(File.ReadAllText(Path.Combine(f.Target, "xRemote.exe")) == "new executable", "Retry after recovery failed");
});
Check("committed crash recovery keeps new files and clears pending state", () =>
{
    var f = Fixture("committed");
    UpdateCore.WriteAtomic(Path.Combine(f.State, "transaction.json"), JsonConvert.SerializeObject(new UpdateTransaction { Committed = true }));
    UpdateCore.Recover(f.Target, f.State);
    Need(!File.Exists(Path.Combine(f.State, "pending.json")), "Committed version would be applied again");
});
Check("installation failure during replacement restores earlier replacements", () =>
{
    var f = Fixture("rollback");
    // A directory where a new component should go fails after the two old binaries were replaced.
    Directory.CreateDirectory(Path.Combine(f.Target, "updater/xRemote.Updater.exe"));
    try { UpdateCore.Apply(f.Target, f.State, publicKey); throw new Exception("Expected installation failure"); }
    catch (IOException) { }
    Need(File.ReadAllText(Path.Combine(f.Target, "xRemote.exe")) == "old executable", "EXE not restored");
    Need(File.ReadAllText(Path.Combine(f.Target, "1Remote.dll")) == "old component", "DLL not restored");
    Need(!File.Exists(Path.Combine(f.State, "transaction.json")), "Rollback unfinished");
});
Check("per-install cache identity is stable and distinct", () =>
{
    Need(UpdateCore.StateDirectory(root) == UpdateCore.StateDirectory(root + "\\"), "Same install got two caches");
    Need(UpdateCore.StateDirectory(root) != UpdateCore.StateDirectory(root + "-other"), "Install caches overlap");
});
Console.WriteLine($"{passed} update checks passed; fixtures: {root}");
