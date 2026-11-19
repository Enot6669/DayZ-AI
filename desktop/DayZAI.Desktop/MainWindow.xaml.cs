using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
namespace DayZAI.Desktop;
public partial class MainWindow : Window
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://127.0.0.1:8765/"), Timeout = TimeSpan.FromSeconds(30) };
    private readonly HttpClient _modelHttp = new() { BaseAddress = new Uri("http://127.0.0.1:8765/"), Timeout = TimeSpan.FromSeconds(135) };
    private readonly string _profilesFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DayZAI", "profiles.json");
    private readonly string _journalFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DayZAI", "operations.jsonl");
    private List<ServerProfile> _profiles = [];
    public MainWindow()
    {
        InitializeComponent();
        try { _profiles = ServerWorkspace.LoadProfiles(_profilesFile); RefreshProfiles(); }
        catch (Exception ex) { Output.Text = $"Не удалось загрузить профили: {ex.Message}"; }
    }
    private void RefreshProfiles(ServerProfile? selected = null)
    {
        Profiles.ItemsSource = null;
        Profiles.ItemsSource = _profiles;
        Profiles.SelectedItem = selected ?? _profiles.FirstOrDefault();
    }
    private void AddProfile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Выберите локальную папку сервера DayZ" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var profile = ServerWorkspace.CreateProfile(dialog.FolderName);
            var existing = _profiles.FirstOrDefault(x => string.Equals(x.Folder, profile.Folder, StringComparison.OrdinalIgnoreCase));
            if (existing != null) { RefreshProfiles(existing); return; }
            var updated = new List<ServerProfile>(_profiles) { profile };
            ServerWorkspace.SaveProfiles(_profilesFile, updated);
            _profiles = updated;
            RefreshProfiles(profile);
            Output.Text = $"Профиль сохранён: {profile.Folder}";
        }
        catch (Exception ex) { Output.Text = $"Ошибка профиля: {ex.Message}"; }
    }
    private async void Launch_Click(object sender, RoutedEventArgs e)
    {
        if (Profiles.SelectedItem is not ServerProfile profile || !int.TryParse(GamePort.Text, out var port))
        { Output.Text = "Выберите профиль и укажите порт."; return; }
        LaunchButton.IsEnabled = false;
        try
        {
            var modList = LaunchMods.Text;
            var plan = await Task.Run(() => ServerLaunch.Prepare(profile, port, modList));
            var mods = plan.Mods.Length == 0 ? "Без модов" : string.Join(';', plan.Mods);
            if (MessageBox.Show($"Запустить доверенный локальный EXE?\n{plan.Executable}\nПорт: {plan.Port}\nМоды: {mods}\nПорядок и зависимости модов не проверены. Запуск может открыть сетевой порт.", "Подтверждение запуска", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            var pid = await Task.Run(() => ServerLaunch.StartConfirmed(plan, _journalFile));
            Output.Text = $"Процесс создан: PID {pid}. Готовность игрового сервера ещё не проверена.\nЖурнал: {_journalFile}";
        }
        catch (Exception ex) { Output.Text = $"Ошибка запуска: {ex.Message}"; }
        finally { LaunchButton.IsEnabled = true; }
    }
    private void Journal_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!File.Exists(_journalFile)) { Output.Text = "Журнал пока пуст."; return; }
            if (new FileInfo(_journalFile).Length > 2_000_000) { Output.Text = $"Журнал больше 2 МБ: {_journalFile}"; return; }
            Output.Text = string.Join(Environment.NewLine, File.ReadLines(_journalFile).TakeLast(100));
        }
        catch (Exception ex) { Output.Text = $"Ошибка чтения журнала: {ex.Message}"; }
    }
    private void Steam_Click(object sender, RoutedEventArgs e)
    {
        if (Profiles.SelectedItem is not ServerProfile profile) { Output.Text = "Создайте профиль с папкой установки сервера (можно пустой)."; return; }
        new SteamWindow(profile, _journalFile) { Owner = this }.ShowDialog();
    }
    private async void InspectProfile_Click(object sender, RoutedEventArgs e)
    {
        if (Profiles.SelectedItem is not ServerProfile profile) { Output.Text = "Сначала добавьте профиль."; return; }
        try
        {
            var result = await Task.Run(() => ServerWorkspace.Inspect(profile));
            var report = new System.Text.StringBuilder();
            report.AppendLine(profile.Folder);
            report.AppendLine($"DayZ Server EXE: {(result.ExecutablePresent ? "найден" : "не найден")}");
            report.AppendLine($"serverDZ.cfg: {(result.ConfigPresent ? "найден" : "не найден")}");
            report.AppendLine($"Модов в папках @: {result.Mods.Count}");
            foreach (var mod in result.Mods) report.AppendLine($"{mod.Name}: PBO {mod.PboCount}, ключи {mod.KeyCount} — {mod.Note}");
            report.AppendLine("Проверено только наличие файлов. Зависимости, подписи PBO и порядок загрузки не проверены. Сервер не изменён.");
            Output.Text = report.ToString();
        }
        catch (Exception ex) { Output.Text = $"Ошибка проверки папки: {ex.Message}"; }
    }
    private async void Backup_Click(object sender, RoutedEventArgs e)
    {
        var source = new OpenFileDialog { Filter = "Конфигурации (*.json;*.xml;*.cfg;*.ini;*.txt)|*.json;*.xml;*.cfg;*.ini;*.txt", CheckFileExists = true };
        if (Profiles.SelectedItem is ServerProfile profile && Directory.Exists(profile.Folder)) source.InitialDirectory = profile.Folder;
        if (source.ShowDialog() != true) return;
        var destination = new SaveFileDialog { Filter = "DayZ backup (*.zip)|*.zip", FileName = Path.GetFileName(source.FileName) + ".backup.zip", DefaultExt = ".zip", AddExtension = true };
        if (destination.ShowDialog() != true) return;
        try
        {
            var metadata = await Task.Run(() => ServerWorkspace.CreateBackup(source.FileName, destination.FileName));
            Output.Text = $"Копия создана: {destination.FileName}\n SHA-256: {metadata.Sha256}\nАрхив может содержать пароли. Храните его локально.";
        }
        catch (Exception ex) { Output.Text = $"Ошибка копирования: {ex.Message}"; }
    }
    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        var source = new OpenFileDialog { Filter = "DayZ backup (*.zip)|*.zip", CheckFileExists = true };
        if (source.ShowDialog() != true) return;
        try
        {
            var backup = await Task.Run(() => ServerWorkspace.ReadBackup(source.FileName));
            var destination = new SaveFileDialog { FileName = "restored-" + backup.Manifest.FileName, Filter = "Файл конфигурации|*.*", AddExtension = false };
            if (destination.ShowDialog() != true) return;
            if (MessageBox.Show($"Контрольная сумма проверена. Создать новый файл?\n{destination.FileName}\nСуществующие файлы не заменяются.", "Восстановление", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            await Task.Run(() => ServerWorkspace.RestoreNewFile(backup.Bytes, destination.FileName));
            Output.Text = $"Восстановлен отдельный файл: {destination.FileName}";
        }
        catch (Exception ex) { Output.Text = $"Ошибка восстановления: {ex.Message}"; }
    }
    private async void Ask_Click(object sender, RoutedEventArgs e)
    {
        var prompt = Prompt.Text.Trim();
        if (prompt.Length == 0) { Output.Text = "Введите вопрос или фрагмент кода."; return; }
        AskButton.IsEnabled = false;
        try
        {
            Output.Text = "Локальная модель готовит ответ…";
            using var response = await _modelHttp.PostAsJsonAsync("ask", new { prompt });
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var answer = json.RootElement.GetProperty("answer").GetString() ?? "Модель вернула пустой ответ.";
            Output.Text = answer + "\n\nПодобранные справочные материалы (не доказательство проверенного ответа):\n" + FormatReferences(json.RootElement.GetProperty("references"));
        }
        catch (Exception ex) { Output.Text = $"ИИ недоступен. Проверьте ядро и Ollama: {ex.Message}"; }
        finally { AskButton.IsEnabled = true; }
    }
    private static string FormatReferences(JsonElement entries)
    {
        if (entries.GetArrayLength() == 0) return "Подходящих материалов не найдено.";
        var report = new System.Text.StringBuilder();
        foreach (var item in entries.EnumerateArray())
        {
            report.AppendLine($"[{item.GetProperty("id").GetString()}] {item.GetProperty("title").GetString()}");
            report.AppendLine(item.GetProperty("summary").GetString());
            report.AppendLine(item.GetProperty("source").GetString());
            report.AppendLine();
        }
        return report.ToString();
    }
    private async void Knowledge_Click(object sender, RoutedEventArgs e)
    {
        var query = Prompt.Text.Trim();
        if (query.Length == 0) { Output.Text = "Введите тему, ошибку или название API для поиска."; return; }
        try
        {
            using var json = JsonDocument.Parse(await _http.GetStringAsync("knowledge?q=" + Uri.EscapeDataString(query)));
            Output.Text = $"База знаний DayZ: {json.RootElement.GetProperty("total_entries").GetInt32()} материалов.\n\n" + FormatReferences(json.RootElement.GetProperty("results"));
        }
        catch (Exception ex) { Output.Text = $"Ошибка поиска: {ex.Message}"; }
    }
    private async void CheckConfig_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Конфигурации (*.json;*.xml)|*.json;*.xml", CheckFileExists = true };
        if (dialog.ShowDialog() != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 2_000_000) { Output.Text = "Файл больше 2 МБ."; return; }
            var kind = Path.GetExtension(dialog.FileName).TrimStart('.').ToLowerInvariant();
            var text = await File.ReadAllTextAsync(dialog.FileName);
            using var response = await _http.PostAsJsonAsync("config/validate", new { kind, text });
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Output.Text = json.RootElement.GetProperty("valid").GetBoolean()
                ? "Синтаксис корректен. Совместимость с DayZ не проверялась."
                : $"Ошибка конфигурации: {json.RootElement.GetProperty("error").GetString()}";
        }
        catch (Exception ex) { Output.Text = $"Ошибка проверки: {ex.Message}"; }
    }
    private async void CheckAI_Click(object sender, RoutedEventArgs e)
    {
        try { Output.Text = await _http.GetStringAsync("health"); }
        catch (Exception ex) { Output.Text = $"ИИ-ядро недоступно: {ex.Message}"; }
    }
    private async void CheckServers_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var response = await _http.GetAsync("status");
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Output.Text = FormatStatus(json.RootElement, "Локальный снимок");
        }
        catch (Exception ex) { Output.Text = $"Не удалось получить состояние сервера: {ex.Message}"; }
    }
    private async void ImportStatus_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Отчёт состояния (*.json)|*.json", CheckFileExists = true };
        if (dialog.ShowDialog() != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 1_000_000) throw new IOException("Отчёт больше 1 МБ.");
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(dialog.FileName));
            Output.Text = FormatStatus(json.RootElement, "Импортированный снимок (не проверен, не онлайн)");
        }
        catch (Exception ex) { Output.Text = $"Ошибка импорта: {ex.Message}"; }
    }
    private static string FormatStatus(JsonElement root, string title)
    {
        if (root.GetProperty("schema").GetString() != "dayz-ai-status-v1") throw new InvalidDataException("Неизвестная схема отчёта.");
        var system = root.GetProperty("system");
        var servers = root.GetProperty("servers");
        var report = new System.Text.StringBuilder();
        report.AppendLine(title);
        report.AppendLine($"Время UTC: {root.GetProperty("captured_utc").GetString()}");
        report.AppendLine($"CPU всего: {system.GetProperty("cpu_percent").GetDouble():F1}%");
        report.AppendLine($"RAM доступно: {system.GetProperty("memory_available_mb").GetInt64()} / {system.GetProperty("memory_total_mb").GetInt64()} МБ");
        report.AppendLine($"Процессов DayZ: {servers.GetArrayLength()}");
        foreach (var item in servers.EnumerateArray().Take(100))
        {
            var memory = item.TryGetProperty("memory_mb", out var value) && value.ValueKind == JsonValueKind.Number ? $"{value.GetDouble():F1} МБ" : "н/д";
            report.AppendLine($"PID {item.GetProperty("pid").GetInt32()} | {item.GetProperty("name").GetString()} | RAM {memory}");
        }
        return report.ToString();
    }
    private async void SelectLog_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "DayZ logs (*.rpt;*.log;*.adm)|*.rpt;*.log;*.adm|Text files (*.txt)|*.txt", CheckFileExists = true };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var info = new FileInfo(dialog.FileName);
            if (info.Length > 2_000_000) { Output.Text = "Файл больше 2 МБ. В первой версии выберите меньший лог."; return; }
            var content = await File.ReadAllTextAsync(dialog.FileName);
            using var response = await _http.PostAsJsonAsync("analyze", new { text = content });
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var findings = json.RootElement.GetProperty("findings");
            if (findings.GetArrayLength() == 0)
            {
                Output.Text = "Подозрительных строк не обнаружено. Это не гарантирует отсутствие проблем.";
                return;
            }
            var report = new System.Text.StringBuilder();
            report.AppendLine($"Найдено сообщений: {findings.GetArrayLength()}");
            report.AppendLine();
            foreach (var item in findings.EnumerateArray())
            {
                var line = item.GetProperty("line").GetInt32();
                var category = item.GetProperty("category").GetString();
                var severity = item.GetProperty("severity").GetString();
                var message = item.GetProperty("message").GetString();
                var advice = item.GetProperty("advice").GetString();
                report.AppendLine($"Строка {line} | {severity} | {category}");
                report.AppendLine(message);
                report.AppendLine($"Рекомендация: {advice}");
                report.AppendLine(new string('─', 48));
            }
            Output.Text = report.ToString();
        }
        catch (Exception ex) { Output.Text = $"Ошибка анализа: {ex.Message}"; }
    }
}
