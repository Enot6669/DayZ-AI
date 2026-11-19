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
    public MainWindow() => InitializeComponent();
    private async void CheckAI_Click(object sender, RoutedEventArgs e)
    {
        try { Output.Text = await _http.GetStringAsync("health"); }
        catch (Exception ex) { Output.Text = $"ИИ-ядро недоступно: {ex.Message}"; }
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
            Output.Text = JsonSerializer.Serialize(json.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex) { Output.Text = $"Ошибка анализа: {ex.Message}"; }
    }
}