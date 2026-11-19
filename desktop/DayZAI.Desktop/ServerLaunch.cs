using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace DayZAI.Desktop;

public sealed record LaunchPlan(string Folder, string Executable, int Port, string[] Mods);

public static class ServerLaunch
{
    public static LaunchPlan Prepare(ServerProfile profile, int port, string modList)
    {
        var folder = ServerWorkspace.CreateProfile(profile.Folder).Folder;
        if (port < 1024 || port > 65531) throw new IOException("Допустимый порт: 1024–65531.");
        var exe = new[] { "DayZServer_x64.exe", "DayZServer.exe" }
            .Select(name => Path.Combine(folder, name)).FirstOrDefault(File.Exists)
            ?? throw new IOException("EXE сервера не найден.");
        var cfg = Path.Combine(folder, "serverDZ.cfg");
        if (!File.Exists(cfg)) throw new IOException("serverDZ.cfg не найден.");
        if (File.GetAttributes(exe).HasFlag(FileAttributes.ReparsePoint) || File.GetAttributes(cfg).HasFlag(FileAttributes.ReparsePoint))
            throw new IOException("Ссылки на EXE и конфигурацию не поддерживаются.");
        var mods = modList.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (mods.Length > 100 || mods.Distinct(StringComparer.OrdinalIgnoreCase).Count() != mods.Length)
            throw new IOException("Список модов содержит дубликаты или больше 100 элементов.");
        var installed = ServerWorkspace.Inspect(profile).Mods;
        foreach (var mod in mods)
        {
            if (!mod.StartsWith('@') || mod.Length < 2 || mod.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || mod.Contains('\\') || mod.Contains('/') || mod.Contains('"'))
                throw new IOException("Указывайте только имена локальных папок @mod.");
            var check = installed.FirstOrDefault(x => string.Equals(x.Name, mod, StringComparison.OrdinalIgnoreCase));
            if (check == null || check.PboCount == 0) throw new IOException($"Мод отсутствует или не содержит PBO: {mod}");
        }
        return new LaunchPlan(folder, exe, port, mods);
    }

    public static ProcessStartInfo BuildStartInfo(LaunchPlan plan)
    {
        var info = new ProcessStartInfo { FileName = plan.Executable, WorkingDirectory = plan.Folder, UseShellExecute = false };
        info.ArgumentList.Add("-config=serverDZ.cfg");
        info.ArgumentList.Add($"-port={plan.Port}");
        if (plan.Mods.Length > 0) info.ArgumentList.Add("-mod=" + string.Join(';', plan.Mods));
        info.ArgumentList.Add("-dologs");
        info.ArgumentList.Add("-adminlog");
        return info;
    }

    public static void Record(string journal, string folder, string action, string result, int? pid = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(journal))!);
        var line = JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow, folder, action, result, pid });
        File.AppendAllText(journal, line + Environment.NewLine);
    }

    public static int StartConfirmed(LaunchPlan approved, string journal)
    {
        using var gate = new System.Threading.Mutex(false, "DayZAI.LocalServerStart");
        if (!gate.WaitOne(TimeSpan.Zero)) throw new IOException("Другой экземпляр приложения уже запускает сервер.");
        try { return StartUnderLock(approved, journal); }
        finally { gate.ReleaseMutex(); }
    }

    private static int StartUnderLock(LaunchPlan approved, string journal)
    {
        // Revalidate at execution time; only one local DayZ process is supported in this preview.
        var plan = Prepare(new ServerProfile("", approved.Folder), approved.Port, string.Join(';', approved.Mods));
        if (!string.Equals(plan.Executable, approved.Executable, StringComparison.OrdinalIgnoreCase))
            throw new IOException("EXE изменился после подтверждения.");
        foreach (var name in new[] { "DayZServer_x64", "DayZServer" })
        {
            var processes = Process.GetProcessesByName(name);
            try { if (processes.Length > 0) throw new IOException("Уже запущен DayZ Server. Повторный запуск запрещён в этой версии."); }
            finally { foreach (var process in processes) process.Dispose(); }
        }
        Record(journal, plan.Folder, "start", "confirmed");
        using var started = Process.Start(BuildStartInfo(plan)) ?? throw new IOException("Не удалось запустить сервер.");
        var pid = started.Id;
        try { Record(journal, plan.Folder, "start", "process_created", pid); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* Process already created; return its PID rather than report a failed launch. */ }
        return pid;
    }
}
