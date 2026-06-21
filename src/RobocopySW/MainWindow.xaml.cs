using System.Windows;
using RobocopySW.Core.Models;
using RobocopySW.ViewModels;

namespace RobocopySW;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly AppHost _host;
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _host = AppHost.Load();
        _vm = new MainViewModel(_host);
        DataContext = _vm;

        // Il log viene riversato a blocchi (sul thread UI) per non ingolfare l'interfaccia.
        _vm.LogFlushed += text =>
        {
            LogBox.AppendText(text);
            LogBox.ScrollToEnd();
        };
        _vm.LogCleared += () => LogBox.Clear();
    }

    private void OnNewJob(object sender, RoutedEventArgs e)
    {
        var job = new BackupJob { Name = "Nuovo job" };
        if (ShowEditor(job))
        {
            _vm.Jobs.Add(new JobViewModel(job));
            _vm.PersistJobs();
        }
    }

    private void OnEditJob(object sender, RoutedEventArgs e)
    {
        var selected = _vm.SelectedJob;
        if (selected is null) return;

        // Modifica su una copia: se l'utente annulla, l'originale resta intatto.
        var clone = Clone(selected.Model);
        if (ShowEditor(clone))
        {
            CopyInto(clone, selected.Model);
            selected.RefreshAll();
            _vm.PersistJobs();
        }
    }

    private void OnDeleteJob(object sender, RoutedEventArgs e)
    {
        var selected = _vm.SelectedJob;
        if (selected is null) return;

        var confirm = MessageBox.Show(
            $"Eliminare il job '{selected.Name}'?", "Conferma",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm == MessageBoxResult.Yes)
        {
            _vm.Jobs.Remove(selected);
            _vm.PersistJobs();
        }
    }

    private void OnSettings(object sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow(_host) { Owner = this };
        if (win.ShowDialog() == true)
            _host.SaveConfig();
    }

    private bool ShowEditor(BackupJob job)
    {
        var win = new JobEditorWindow(job, _host.Config.Credentials) { Owner = this };
        return win.ShowDialog() == true;
    }

    private static BackupJob Clone(BackupJob j) => new()
    {
        Name = j.Name,
        Source = j.Source,
        Destination = j.Destination,
        Mirror = j.Mirror,
        ExcludeOlder = j.ExcludeOlder,
        CopyAll = j.CopyAll,
        MultiThread = j.MultiThread,
        UnbufferedIO = j.UnbufferedIO,
        Restartable = j.Restartable,
        ExcludeFiles = new List<string>(j.ExcludeFiles),
        ExcludeDirs = new List<string>(j.ExcludeDirs),
        Retries = j.Retries,
        Wait = j.Wait,
        Enabled = j.Enabled,
        CredentialId = j.CredentialId,
    };

    private static void CopyInto(BackupJob from, BackupJob to)
    {
        to.Name = from.Name;
        to.Source = from.Source;
        to.Destination = from.Destination;
        to.Mirror = from.Mirror;
        to.ExcludeOlder = from.ExcludeOlder;
        to.CopyAll = from.CopyAll;
        to.MultiThread = from.MultiThread;
        to.UnbufferedIO = from.UnbufferedIO;
        to.Restartable = from.Restartable;
        to.ExcludeFiles = new List<string>(from.ExcludeFiles);
        to.ExcludeDirs = new List<string>(from.ExcludeDirs);
        to.Retries = from.Retries;
        to.Wait = from.Wait;
        to.Enabled = from.Enabled;
        to.CredentialId = from.CredentialId;
    }
}
