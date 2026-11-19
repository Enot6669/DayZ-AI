using System.IO.Compression;
using DayZAI.Desktop;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static void Reject(Action action, string message)
{
    try { action(); }
    catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException) { return; }
    throw new Exception(message);
}

var root = Directory.CreateTempSubdirectory("dayz-ai-test-").FullName;
try
{
    var server = Directory.CreateDirectory(Path.Combine(root, "server")).FullName;
    var mod = Directory.CreateDirectory(Path.Combine(server, "@Example", "addons")).FullName;
    File.WriteAllText(Path.Combine(mod, "sample.pbo"), "test");
    Directory.CreateDirectory(Path.Combine(server, "@Empty"));
    var cfg = Path.Combine(server, "serverDZ.cfg");
    File.WriteAllText(cfg, "hostname = \"Test\";");
    File.WriteAllText(Path.Combine(server, "DayZServer_x64.exe"), "placeholder");
    var profile = ServerWorkspace.CreateProfile(server);
    var settings = Path.Combine(root, "data", "profiles.json");
    ServerWorkspace.SaveProfiles(settings, [profile]);
    Check(ServerWorkspace.LoadProfiles(settings).Single() == profile, "Profile round trip failed");
    ServerWorkspace.SaveProfiles(settings, []);
    Check(ServerWorkspace.LoadProfiles(settings).Count == 0, "Atomic profile update failed");
    Reject(() => ServerWorkspace.CreateProfile(Path.GetPathRoot(root)!), "Disk root accepted");

    var inspection = ServerWorkspace.Inspect(profile);
    Check(inspection.ExecutablePresent && inspection.ConfigPresent, "Server files missing");
    Check(inspection.Mods.Count == 2, "Incorrect mod inventory");
    Check(inspection.Mods.Single(x => x.Name == "@Example").PboCount == 1, "PBO count wrong");
    Check(inspection.Mods.Single(x => x.Name == "@Empty").PboCount == 0, "Empty mod not found");

    var plan = ServerLaunch.Prepare(profile, 2302, "@Example");
    var startInfo = ServerLaunch.BuildStartInfo(plan);
    Check(!startInfo.UseShellExecute && startInfo.FileName == Path.Combine(server, "DayZServer_x64.exe"), "Unexpected executable");
    Check(startInfo.ArgumentList.Contains("-mod=@Example") && startInfo.ArgumentList.Contains("-port=2302"), "Launch arguments missing");
    Reject(() => ServerLaunch.Prepare(profile, 0, ""), "Invalid port accepted");
    Reject(() => ServerLaunch.Prepare(profile, 2302, "@Unknown"), "Missing mod accepted");
    Reject(() => ServerLaunch.Prepare(profile, 2302, "@Empty"), "Empty mod accepted");
    Reject(() => ServerLaunch.Prepare(profile, 2302, "@Example;@Example"), "Duplicate mod accepted");
    Reject(() => ServerLaunch.Prepare(profile, 2302, "@Example;-config=other.cfg"), "Free-form argument accepted");
    var journal = Path.Combine(root, "data", "operations.jsonl");
    ServerLaunch.Record(journal, server, "start", "test", 123);
    using (var record = System.Text.Json.JsonDocument.Parse(File.ReadAllText(journal)))
        Check(record.RootElement.GetProperty("pid").GetInt32() == 123, "Journal round trip failed");

    var archive = Path.Combine(root, "backup.zip");
    var created = ServerWorkspace.CreateBackup(cfg, archive);
    var backup = ServerWorkspace.ReadBackup(archive);
    Check(created == backup.Manifest, "Backup metadata mismatch");
    var restored = Path.Combine(root, "restored.cfg");
    ServerWorkspace.RestoreNewFile(backup.Bytes, restored);
    Check(File.ReadAllBytes(restored).SequenceEqual(File.ReadAllBytes(cfg)), "Restored file differs");
    Reject(() => ServerWorkspace.CreateBackup(cfg, archive), "Existing archive overwritten");
    Reject(() => ServerWorkspace.RestoreNewFile(backup.Bytes, cfg), "Existing config overwritten");
    Check(File.ReadAllText(cfg) == "hostname = \"Test\";", "Original config changed");

    using (var zip = ZipFile.Open(archive, ZipArchiveMode.Update))
    {
        zip.GetEntry("config.bin")!.Delete();
        using var entry = zip.CreateEntry("config.bin").Open();
        entry.WriteByte(0);
    }
    Reject(() => ServerWorkspace.ReadBackup(archive), "Corrupted archive accepted");
    var oversized = Path.Combine(root, "large.json");
    using (var large = File.Create(oversized)) large.SetLength(ServerWorkspace.MaxConfigBytes + 1);
    Reject(() => ServerWorkspace.CreateBackup(oversized, Path.Combine(root, "large.zip")), "Oversized file accepted");
    Console.WriteLine("All workspace tests passed.");
}
finally { Directory.Delete(root, recursive: true); }
