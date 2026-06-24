using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RobocopySW.Core.Models;
using RobocopySW.Core.Services;
using RobocopySW.ViewModels;

namespace RobocopySW;

public partial class SettingsWindow : Wpf.Ui.Controls.FluentWindow
{
    private const string ScheduledTaskName = "AvviaTutti";
    private readonly SettingsViewModel _vm;
    private readonly SchedulerService _scheduler = new();

    public SettingsWindow(AppHost host)
    {
        InitializeComponent();
        _vm = new SettingsViewModel(host.Config.Settings, host.Config.Credentials, host.Credentials);
        DataContext = _vm;

        // Mostra il percorso predefinito (grigio) quando il campo è vuoto: chiarisce che
        // i log ci sono comunque, nelle sottocartelle accanto all'app.
        LogRootBox.PlaceholderText = LogService.DefaultLogRoot;
        TempRootBox.PlaceholderText = LogService.DefaultTempRoot;
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
        var dlg = new OpenFolderDialog { Title = "Seleziona cartella" };
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
            MessageBox.Show("L'Id della credenziale è obbligatorio.", "Dati mancanti",
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
                MessageBox.Show("Orario non valido. Usa il formato HH:mm.", "Pianificazione",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var exe = Environment.ProcessPath
                ?? throw new InvalidOperationException("Percorso eseguibile non disponibile.");
            var freq = ScheduleFreq.SelectedIndex == 1
                ? ScheduleFrequency.Weekly
                : ScheduleFrequency.Daily;

            _scheduler.CreateOrUpdate(ScheduledTaskName, exe, "--run-all", freq, time);
            ScheduleStatus.Foreground = System.Windows.Media.Brushes.Green;
            ScheduleStatus.Text = $"Attività pianificata creata/aggiornata per le {time:HH:mm}.";
        }
        catch (Exception ex)
        {
            ScheduleStatus.Foreground = System.Windows.Media.Brushes.Red;
            ScheduleStatus.Text = "Errore: " + ex.Message;
        }
    }

    private void OnRemoveSchedule(object sender, RoutedEventArgs e)
    {
        try
        {
            _scheduler.Delete(ScheduledTaskName);
            ScheduleStatus.Foreground = System.Windows.Media.Brushes.Green;
            ScheduleStatus.Text = "Attività pianificata rimossa.";
        }
        catch (Exception ex)
        {
            ScheduleStatus.Foreground = System.Windows.Media.Brushes.Red;
            ScheduleStatus.Text = "Errore: " + ex.Message;
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
