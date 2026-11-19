using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace DayZAI.Desktop;

public partial class SteamWindow : Window
{
    private readonly ServerProfile _profile;
    private readonly string _journal;
    private bool _busy;

    public SteamWindow(ServerProfile profile, string journal)
    {
        InitializeComponent();
        _profile = profile;
        _journal = journal;
        Target.Text = "Сервер: " + profile.Folder;
        Closing += (_, e) => { if (_busy) { e.Cancel = true; Result.Text = "Дождитесь завершения операции в SteamCMD."; } };
    }

    private void Choose_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "SteamCMD|steamcmd.exe", CheckFileExists = true };
        if (dialog.ShowDialog() == true) SteamPath.Text = dialog.FileName;
    }

    private void DownloadLink_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip") { UseShellExecute = true }); }
        catch (Exception ex) { Result.Text = ex.Message; }
    }

    private void SetBusy(bool value)
    {
        _busy = value;
        ServerButton.IsEnabled = DownloadButton.IsEnabled = InstallButton.IsEnabled = !value;
    }

    private async void Server_Click(object sender, RoutedEventArgs e) => await DownloadAsync(null);
    private async void Workshop_Click(object sender, RoutedEventArgs e)
    {
        try { await DownloadAsync(SteamManager.ValidateWorkshopId(WorkshopId.Text)); }
        catch (Exception ex) { Result.Text = ex.Message; }
    }

    private async Task DownloadAsync(string? id)
    {
        try
        {
            var plan = SteamManager.Prepare(SteamPath.Text, _profile, Account.Text, id);
            var action = id == null ? "steam_server_update" : "steam_workshop_download";
            var target = id == null ? "DayZ Server 223350" : "Workshop " + id;
            if (MessageBox.Show($"Запустить SteamCMD для {target}?\nПапка: {_profile.Folder}\nОперация изменит файлы. Убедитесь, что сервер остановлен и резервные копии сделаны.\nАвторизация и Steam Guard выполняются в SteamCMD.", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            SetBusy(true);
            ServerLaunch.Record(_journal, _profile.Folder, action, "confirmed");
            Result.Text = "SteamCMD запущен. Следуйте инструкциям в его окне.";
            var code = await Task.Run(() => SteamManager.RunConfirmed(plan));
            ServerLaunch.Record(_journal, _profile.Folder, action, "exit_" + code);
            var found = id == null ? File.Exists(Path.Combine(_profile.Folder, "DayZServer_x64.exe")) : Directory.Exists(Path.Combine(_profile.Folder, "steamapps", "workshop", "content", SteamManager.WorkshopAppId, id));
            Result.Text = $"SteamCMD завершён с кодом {code}. Файлы в папке сервера: {(found ? "обнаружены" : "не обнаружены")}. Код выхода и наличие старых файлов не гарантируют успешную загрузку. Проверьте сообщения SteamCMD.";
        }
        catch (Exception ex) { Result.Text = "Ошибка SteamCMD: " + ex.Message; }
        finally { SetBusy(false); }
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var id = SteamManager.ValidateWorkshopId(WorkshopId.Text);
            var steamcmd = SteamPath.Text;
            var source = SteamManager.WorkshopSource(steamcmd, _profile, id);
            if (MessageBox.Show($"Установить мод из {source}?\nБудут созданы папка @Workshop_{id} и отсутствующие публичные ключи. Существующие файлы не заменяются. Доверяйте только проверенным модам.", "Подтверждение установки", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            SetBusy(true);
            ServerLaunch.Record(_journal, _profile.Folder, "steam_workshop_install", "confirmed");
            var installed = await Task.Run(() => SteamManager.InstallNewModConfirmed(steamcmd, _profile, id));
            ServerLaunch.Record(_journal, _profile.Folder, "steam_workshop_install", "created");
            Result.Text = $"Установлено: {installed}\nДобавьте @Workshop_{id} в список запуска. Зависимости и совместимость не проверены.";
        }
        catch (Exception ex) { Result.Text = "Ошибка установки: " + ex.Message; }
        finally { SetBusy(false); }
    }
}
