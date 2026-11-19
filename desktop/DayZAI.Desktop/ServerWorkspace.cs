using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace DayZAI.Desktop;

public sealed record ServerProfile(string Name, string Folder);
public sealed record ModCheck(string Name, int PboCount, int KeyCount, string Note);
public sealed record ServerInspection(bool ExecutablePresent, bool ConfigPresent, List<ModCheck> Mods);
public sealed record BackupManifest(string FileName, string Sha256, long Size, DateTimeOffset CreatedUtc);

public static class ServerWorkspace
{
    public const int MaxConfigBytes = 10 * 1024 * 1024;
    private static readonly string[] ConfigExtensions = [".json", ".xml", ".cfg", ".ini", ".txt"];

    public static ServerProfile CreateProfile(string folder)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        if (!Directory.Exists(full) || full == Path.GetPathRoot(full) || full.StartsWith("\\\\"))
            throw new IOException("Выберите существующую локальную папку сервера, не корень диска.");
        return new ServerProfile(Path.GetFileName(full), full);
    }

    public static List<ServerProfile> LoadProfiles(string file) => File.Exists(file)
        ? JsonSerializer.Deserialize<List<ServerProfile>>(File.ReadAllText(file)) ?? [] : [];

    public static void SaveProfiles(string file, List<ServerProfile> profiles)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        var temp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write))
                JsonSerializer.Serialize(stream, profiles);
            File.Move(temp, file, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static ServerInspection Inspect(ServerProfile profile)
    {
        var folder = CreateProfile(profile.Folder).Folder;
        var options = new EnumerationOptions { RecurseSubdirectories = false, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false };
        var mods = new List<ModCheck>();
        foreach (var mod in Directory.EnumerateDirectories(folder, "@*", options).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            int Count(string subfolder, string pattern)
            {
                var dir = Path.Combine(mod, subfolder);
                return Directory.Exists(dir) && !File.GetAttributes(dir).HasFlag(FileAttributes.ReparsePoint)
                    ? Directory.EnumerateFiles(dir, pattern, options).Count() : 0;
            }
            var pbo = Count("addons", "*.pbo");
            var keys = Count("keys", "*.bikey");
            mods.Add(new ModCheck(Path.GetFileName(mod), pbo, keys, pbo == 0 ? "PBO не обнаружены" : keys == 0 ? "Нет ключей в папке keys" : "Файлы присутствуют"));
        }
        return new ServerInspection(File.Exists(Path.Combine(folder, "DayZServer_x64.exe")) || File.Exists(Path.Combine(folder, "DayZServer.exe")), File.Exists(Path.Combine(folder, "serverDZ.cfg")), mods);
    }

    private static byte[] ReadBounded(Stream input)
    {
        using var buffer = new MemoryStream();
        var block = new byte[8192];
        int read;
        while ((read = input.Read(block)) > 0)
        {
            if (buffer.Length + read > MaxConfigBytes) throw new IOException("Файл превышает 10 МБ.");
            buffer.Write(block, 0, read);
        }
        return buffer.ToArray();
    }

    public static BackupManifest CreateBackup(string source, string archive)
    {
        if (!ConfigExtensions.Contains(Path.GetExtension(source), StringComparer.OrdinalIgnoreCase))
            throw new IOException("Неподдерживаемый формат конфигурации.");
        byte[] bytes;
        using (var input = File.OpenRead(source)) bytes = ReadBounded(input);
        var manifest = new BackupManifest(Path.GetFileName(source), Convert.ToHexString(SHA256.HashData(bytes)), bytes.LongLength, DateTimeOffset.UtcNow);
        using var output = new FileStream(archive, FileMode.CreateNew, FileAccess.Write);
        using var zip = new ZipArchive(output, ZipArchiveMode.Create);
        using (var entry = zip.CreateEntry("manifest.json").Open()) JsonSerializer.Serialize(entry, manifest);
        using (var entry = zip.CreateEntry("config.bin").Open()) entry.Write(bytes);
        return manifest;
    }

    public static (BackupManifest Manifest, byte[] Bytes) ReadBackup(string archive)
    {
        using var zip = ZipFile.OpenRead(archive);
        if (zip.Entries.Count != 2 || zip.Entries.Count(x => x.FullName == "manifest.json") != 1 || zip.Entries.Count(x => x.FullName == "config.bin") != 1)
            throw new InvalidDataException("Неизвестный формат резервной копии.");
        var metadata = zip.GetEntry("manifest.json")!;
        if (metadata.Length > 8192) throw new InvalidDataException("Слишком большой манифест.");
        BackupManifest manifest;
        using (var input = metadata.Open()) manifest = JsonSerializer.Deserialize<BackupManifest>(ReadBounded(input)) ?? throw new InvalidDataException("Нет манифеста.");
        if (string.IsNullOrWhiteSpace(manifest.FileName) || manifest.FileName != Path.GetFileName(manifest.FileName) || manifest.FileName.Contains('\\') || manifest.FileName.Contains('/') || !ConfigExtensions.Contains(Path.GetExtension(manifest.FileName), StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("Некорректное имя файла в резервной копии.");
        byte[] bytes;
        using (var input = zip.GetEntry("config.bin")!.Open()) bytes = ReadBounded(input);
        if (manifest.Size != bytes.LongLength || !string.Equals(manifest.Sha256, Convert.ToHexString(SHA256.HashData(bytes)), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Контрольная сумма резервной копии не совпадает.");
        return (manifest, bytes);
    }

    public static void RestoreNewFile(byte[] verifiedBytes, string destination)
    {
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write);
        output.Write(verifiedBytes);
    }
}
