using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace DayZAI.Desktop;

public static class SteamManager
{
    public const string ServerAppId = "223350";
    public const string WorkshopAppId = "221100";

    public static string ValidateWorkshopId(string value)
    {
        value = value.Trim();
        if (!Regex.IsMatch(value, @"\A[0-9]{1,20}\z") || !ulong.TryParse(value, out var id) || id == 0)
            throw new IOException("Укажите числовой Workshop ID из ссылки на мод.");
        return id.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public static ProcessStartInfo Prepare(string steamcmd, ServerProfile profile, string account, string? workshopId = null)
    {
        var folder = ServerWorkspace.CreateProfile(profile.Folder).Folder;
        steamcmd = Path.GetFullPath(steamcmd);
        if (!File.Exists(steamcmd) || !string.Equals(Path.GetFileName(steamcmd), "steamcmd.exe", StringComparison.OrdinalIgnoreCase) || File.GetAttributes(steamcmd).HasFlag(FileAttributes.ReparsePoint))
            throw new IOException("Выберите официальный steamcmd.exe.");
        account = account.Trim();
        if (!Regex.IsMatch(account, @"\A[A-Za-z0-9_]{1,64}\z") || account.Equals("anonymous", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Укажите имя учётной записи Steam (не пароль). Анонимный вход не поддерживается.");
        var info = new ProcessStartInfo { FileName = steamcmd, WorkingDirectory = Path.GetDirectoryName(steamcmd)!, UseShellExecute = false };
        foreach (var arg in new[] { "+force_install_dir", folder, "+login", account }) info.ArgumentList.Add(arg);
        if (workshopId == null)
        {
            info.ArgumentList.Add("+app_update");
            info.ArgumentList.Add(ServerAppId);
        }
        else
        {
            info.ArgumentList.Add("+workshop_download_item");
            info.ArgumentList.Add(WorkshopAppId);
            info.ArgumentList.Add(ValidateWorkshopId(workshopId));
        }
        info.ArgumentList.Add("+quit");
        return info;
    }

    private static void RequireStopped()
    {
        foreach (var name in new[] { "DayZServer_x64", "DayZServer" })
        {
            var existing = Process.GetProcessesByName(name);
            try { if (existing.Length > 0) throw new IOException("Остановите все локальные DayZ Server через штатную панель перед изменением файлов."); }
            finally { foreach (var process in existing) process.Dispose(); }
        }
    }

    private static T WithMaintenanceLock<T>(Func<T> action)
    {
        using var gate = new System.Threading.Mutex(false, "DayZAI.LocalServerStart");
        if (!gate.WaitOne(TimeSpan.Zero)) throw new IOException("Уже выполняется запуск или обслуживание сервера.");
        try { RequireStopped(); return action(); }
        finally { gate.ReleaseMutex(); }
    }

    public static int RunConfirmed(ProcessStartInfo info) => WithMaintenanceLock(() =>
    {
        // SteamCMD itself handles login and Steam Guard in its own console. Do not redirect its input or output.
        using var process = Process.Start(info) ?? throw new IOException("Не удалось запустить SteamCMD.");
        process.WaitForExit();
        return process.ExitCode;
    });

    public static string WorkshopSource(string steamcmd, ServerProfile profile, string workshopId)
    {
        var id = ValidateWorkshopId(workshopId);
        var folder = ServerWorkspace.CreateProfile(profile.Folder).Folder;
        var relative = Path.Combine("steamapps", "workshop", "content", WorkshopAppId, id);
        var candidate = Path.Combine(folder, relative);
        if (Directory.Exists(candidate)) return candidate;
        var fallback = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(steamcmd))!, relative);
        if (Directory.Exists(fallback)) return fallback;
        throw new IOException("Контент Workshop не найден в папке сервера или SteamCMD.");
    }

    private static void NoLink(string path)
    {
        if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Ссылки и junction внутри модов запрещены.");
    }

    private static void CopyTree(string source, string target)
    {
        NoLink(source);
        Directory.CreateDirectory(target);
        foreach (var directory in Directory.EnumerateDirectories(source)) CopyTree(directory, Path.Combine(target, Path.GetFileName(directory)));
        foreach (var file in Directory.EnumerateFiles(source))
        {
            NoLink(file);
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: false);
        }
    }

    private static bool SameFile(string first, string second)
    {
        NoLink(first); NoLink(second);
        using var a = File.OpenRead(first);
        using var b = File.OpenRead(second);
        return SHA256.HashData(a).SequenceEqual(SHA256.HashData(b));
    }

    public static string InstallNewModConfirmed(string steamcmd, ServerProfile profile, string workshopId) => WithMaintenanceLock(() =>
    {
        var id = ValidateWorkshopId(workshopId);
        var root = ServerWorkspace.CreateProfile(profile.Folder).Folder;
        NoLink(root);
        var source = WorkshopSource(steamcmd, profile, id);
        var target = Path.Combine(root, "@Workshop_" + id);
        if (Directory.Exists(target) || File.Exists(target)) throw new IOException("Мод уже существует. Перезапись запрещена.");
        var addons = Path.Combine(source, "addons");
        if (!Directory.Exists(addons)) throw new IOException("В загруженном моде нет папки addons.");
        NoLink(addons);
        if (!Directory.EnumerateFiles(addons, "*.pbo").Any()) throw new IOException("В загруженном моде нет PBO.");
        var stage = Path.Combine(root, ".dayzai-stage-" + Guid.NewGuid().ToString("N"));
        // Stage the content before making it visible as an installed mod.
        CopyTree(source, stage);
        var modKeys = Path.Combine(stage, "keys");
        var serverKeys = Path.Combine(root, "keys");
        if (Directory.Exists(modKeys))
        {
            if (Directory.Exists(serverKeys)) NoLink(serverKeys);
            var keys = Directory.EnumerateFiles(modKeys, "*.bikey").ToArray();
            foreach (var key in keys)
            {
                var dest = Path.Combine(serverKeys, Path.GetFileName(key));
                if (File.Exists(dest) && !SameFile(key, dest)) throw new IOException("Конфликт ключа: " + Path.GetFileName(key));
            }
            Directory.CreateDirectory(serverKeys);
            foreach (var key in keys)
            {
                var dest = Path.Combine(serverKeys, Path.GetFileName(key));
                if (!File.Exists(dest)) File.Copy(key, dest, overwrite: false);
            }
        }
        RequireStopped();
        Directory.Move(stage, target);
        return target;
    });
}
