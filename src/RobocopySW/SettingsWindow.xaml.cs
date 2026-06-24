using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RobocopySW.Core.Models;
using RobocopySW.Core.Services;
using RobocopySW.Localization;
using RobocopySW.ViewModels;

namespace RobocopySW;

public partial class SettingsWindow : Wpf.Ui.Controls.FluentWindow
{
    private const string ScheduledTaskName = "AvviaTutti";
    private readonly SettingsViewModel _vm;
    private readonly SchedulerService _scheduler = new();
    private readonly CredentialService _credentials;

    public SettingsWindow(AppHost host)
    {
        InitializeComponent();
        _credentials = host.Credentials;
        _vm = new SettingsViewModel(host.Config.Settings, host.Config.Credentials, host.Credentials);
        DataContext = _vm;

        // Mostra il percorso predefinito (grigio) quando il campo è vuoto: chiarisce che
        // i log ci sono comunque, nelle sottocartelle accanto all'app.
        LogRootBox.PlaceholderText = LogService.DefaultLogRoot;
        TempRootBox.PlaceholderText = LogService.DefaultTempRoot;

        RefreshScheduleStatus();
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

    private void OnSave(object sender, RoutedEventArgs e)
    {
        _vm.EmailPasswordPlain = EmailPasswordBox.Password;
        _vm.Commit();
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
