using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RoboKeep.Core.Models;
using RoboKeep.Core.Services;
using RoboKeep.Localization;
using RoboKeep.ViewModels;

namespace RoboKeep;

public partial class SettingsWindow : Wpf.Ui.Controls.FluentWindow
{
    private const string ScheduledTaskName = "AvviaTutti";
    private readonly AppHost _host;
    private readonly SettingsViewModel _vm;
    private readonly SchedulerService _scheduler = new();
    private readonly CredentialService _credentials;

    public SettingsWindow(AppHost host)
    {
        InitializeComponent();
        _host = host;
        _credentials = host.Credentials;
        _vm = new SettingsViewModel(host.Config.Settings, host.Config.Credentials, host.Credentials);
        DataContext = _vm;

        // Mostra il percorso predefinito (grigio) quando il campo è vuoto: chiarisce che
        // i log ci sono comunque, nelle sottocartelle accanto all'app.
        LogRootBox.PlaceholderText = LogService.DefaultLogRoot;
        TempRootBox.PlaceholderText = LogService.DefaultTempRoot;

        RefreshScheduleStatus();

        // Versione mostrata nella scheda Info (es. 1.0.0), letta dai metadati dell'assembly.
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        AppVersionText.Text = v is null ? "1.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    // Apre un link esterno (es. il repository) nel browser predefinito.
    private void OnOpenLink(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch { /* nessun browser/URL non valido: ignora */ }
        e.Handled = true;
    }

    // Mostra la prossima esecuzione pianificata (o "nessuna pianificazione").
    private void RefreshScheduleStatus()
    {
        var next = _scheduler.GetNextRunTime(ScheduledTaskName);
        ScheduleInfo.Text = next is null
            ? Loc.Instance["Sched_None"]
            : string.Format(Loc.Instance["Sched_Next"], next);
    }

    private void OnBrowseLog(object sender, RoutedEventArgs e)
    {
        var p = BrowseFolder(_vm.LogRoot);
        if (p is not null) _vm.LogRoot = p;
    }

    private void OnBrowseTemp(object sender, RoutedEventArgs e)
    {
        var p = BrowseFolder(_vm.TempRoot);
        if (p is not null) _vm.TempRoot = p;
    }

    private static string? BrowseFolder(string? initial)
    {
        var dlg = new OpenFolderDialog { Title = Loc.Instance["Editor_BrowseTitle"] };
        if (!string.IsNullOrWhiteSpace(initial) && Directory.Exists(initial))
            dlg.InitialDirectory = initial;
        return dlg.ShowDialog() == true ? dlg.FolderName : null;
    }

    private void OnCredentialSelected(object sender, SelectionChangedEventArgs e)
    {
        if (CredGrid.SelectedItem is CredentialEntry c)
        {
            CredId.Text = c.Id;
            CredHost.Text = c.Host;
            CredUser.Text = c.User;
            CredPassword.Clear();
        }
    }

    private void OnUpsertCredential(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CredId.Text))
        {
            MessageBox.Show(Loc.Instance["Cred_NameRequired"], Loc.Instance["Common_MissingData"],
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _vm.UpsertCredential(CredId.Text.Trim(), CredHost.Text.Trim(), CredUser.Text.Trim(), CredPassword.Password);
        CredGrid.Items.Refresh();
        CredPassword.Clear();
    }

    private void OnRemoveCredential(object sender, RoutedEventArgs e)
    {
        if (CredGrid.SelectedItem is CredentialEntry c)
        {
            _vm.RemoveCredential(c);
            CredGrid.Items.Refresh();
        }
    }

    private void OnCreateSchedule(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!TimeOnly.TryParse(ScheduleTime.Text, out var time))
            {
                MessageBox.Show(Loc.Instance["Sched_BadTime"], Loc.Instance["Tab_Schedule"],
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var exe = Environment.ProcessPath
                ?? throw new InvalidOperationException("Percorso eseguibile non disponibile.");
            var freq = ScheduleFreq.SelectedIndex switch
            {
                1 => ScheduleFrequency.Weekly,
                2 => ScheduleFrequency.Once,
                _ => ScheduleFrequency.Daily,
            };

            _scheduler.CreateOrUpdate(ScheduledTaskName, exe, "--run-all", freq, time);
            ScheduleStatus.Foreground = System.Windows.Media.Brushes.Green;
            ScheduleStatus.Text = string.Format(Loc.Instance["Sched_Created"], time.ToString("HH:mm"));
            RefreshScheduleStatus();
        }
        catch (Exception ex)
        {
            ScheduleStatus.Foreground = System.Windows.Media.Brushes.Red;
            ScheduleStatus.Text = string.Format(Loc.Instance["Sched_Error"], ex.Message);
        }
    }

    private void OnRemoveSchedule(object sender, RoutedEventArgs e)
    {
        try
        {
            _scheduler.Delete(ScheduledTaskName);
            ScheduleStatus.Foreground = System.Windows.Media.Brushes.Green;
            ScheduleStatus.Text = Loc.Instance["Sched_Removed"];
            RefreshScheduleStatus();
        }
        catch (Exception ex)
        {
            ScheduleStatus.Foreground = System.Windows.Media.Brushes.Red;
            ScheduleStatus.Text = string.Format(Loc.Instance["Sched_Error"], ex.Message);
        }
    }

    private async void OnTestCredential(object sender, RoutedEventArgs e)
    {
        var host = CredHost.Text?.Trim() ?? "";
        var user = CredUser.Text?.Trim() ?? "";
        var pwd = CredPassword.Password;

        CredTestStatus.Foreground = System.Windows.Media.Brushes.Gray;
        CredTestStatus.Text = Loc.Instance["Conn_Testing"];
        TestCredButton.IsEnabled = false;
        try
        {
            var code = await Task.Run(() => _credentials.TryConnect(host, user, pwd));
            if (code == 0)
            {
                CredTestStatus.Foreground = System.Windows.Media.Brushes.Green;
                CredTestStatus.Text = Loc.Instance["Conn_Ok"];
            }
            else
            {
                CredTestStatus.Foreground = System.Windows.Media.Brushes.Red;
                CredTestStatus.Text = ConnErrorMessage(code);
            }
        }
        finally
        {
            TestCredButton.IsEnabled = true;
        }
    }

    private static string ConnErrorMessage(int code) => code switch
    {
        1326 or 86 => Loc.Instance["Conn_LogonFail"],
        53 => Loc.Instance["Conn_PathNotFound"],
        67 => Loc.Instance["Conn_NameNotFound"],
        5 => Loc.Instance["Conn_AccessDenied"],
        _ => string.Format(Loc.Instance["Conn_Generic"], code),
    };

    private async void OnSendTestEmail(object sender, RoutedEventArgs e)
    {
        EmailTestStatus.Foreground = System.Windows.Media.Brushes.Gray;
        EmailTestStatus.Text = Loc.Instance["Email_TestSending"];
        TestEmailButton.IsEnabled = false;
        try
        {
            var email = new EmailService(_credentials);
            // Usa la password appena digitata (se presente), altrimenti quella già salvata.
            await email.SendTestAsync(_vm.EmailSettings, EmailPasswordBox.Password);
            EmailTestStatus.Foreground = System.Windows.Media.Brushes.Green;
            EmailTestStatus.Text = Loc.Instance["Email_TestSent"];
        }
        catch (Exception ex)
        {
            EmailTestStatus.Foreground = System.Windows.Media.Brushes.Red;
            var msg = ex.InnerException?.Message ?? ex.Message;
            EmailTestStatus.Text = string.Format(Loc.Instance["Common_Error"], msg);
        }
        finally
        {
            TestEmailButton.IsEnabled = true;
        }
    }

    private void OnExportConfig(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = Loc.Instance["Cfg_Export"],
            FileName = $"robokeep-config-{DateTime.Now:yyyyMMdd}.json",
            Filter = "JSON|*.json",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            ConfigTransfer.Export(_host.Config, dlg.FileName);
            ConfigTransferStatus.Foreground = System.Windows.Media.Brushes.Green;
            ConfigTransferStatus.Text = string.Format(Loc.Instance["Cfg_Exported"], dlg.FileName);
        }
        catch (Exception ex)
        {
            ConfigTransferStatus.Foreground = System.Windows.Media.Brushes.Red;
            ConfigTransferStatus.Text = string.Format(Loc.Instance["Common_Error"], ex.Message);
        }
    }

    private void OnImportConfig(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = Loc.Instance["Cfg_Import"], Filter = "JSON|*.json" };
        if (dlg.ShowDialog() != true) return;

        var confirm = MessageBox.Show(Loc.Instance["Cfg_ImportConfirm"], Loc.Instance["Cfg_Import"],
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            var imported = ConfigTransfer.Import(dlg.FileName); // valida PRIMA di toccare qualsiasi cosa

            // Nomi dei job PRIMA della sostituzione: le attività pianificate dei job che
            // spariscono con l'import vanno rimosse, altrimenti restano orfane in Windows.
            var oldNames = _host.Config.Jobs.Select(j => j.Name).ToHashSet();

            // Backup della config attuale, poi sostituzione e salvataggio.
            var backupPath = Path.Combine(_host.Store.DirectoryPath,
                $"config.backup-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            ConfigTransfer.Export(_host.Config, backupPath);

            _host.Config.Settings = imported.Settings;
            _host.Config.Credentials = imported.Credentials;
            _host.Config.Jobs = imported.Jobs;
            _host.SaveConfig();

            // Risincronizza le attività per-job con la nuova configurazione e rimuovi
            // quelle dei job non più presenti.
            var exe = Environment.ProcessPath;
            if (exe is not null)
            {
                var scheduler = new SchedulerService();
                foreach (var job in _host.Config.Jobs)
                {
                    try { scheduler.SyncJobTask(job, exe); } catch { }
                }
                foreach (var gone in oldNames.Except(_host.Config.Jobs.Select(j => j.Name)))
                {
                    try { scheduler.RemoveJobTask(gone); } catch { }
                }
            }

            ConfigTransferStatus.Foreground = System.Windows.Media.Brushes.Green;
            ConfigTransferStatus.Text = string.Format(Loc.Instance["Cfg_Imported"], backupPath);
            DialogResult = true; // chiude: la MainWindow ricarica i job
        }
        catch (Exception ex)
        {
            ConfigTransferStatus.Foreground = System.Windows.Media.Brushes.Red;
            ConfigTransferStatus.Text = string.Format(Loc.Instance["Common_Error"], ex.Message);
        }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        _vm.EmailPasswordPlain = EmailPasswordBox.Password;
        _vm.Commit();
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
