using System.IO;
using System.Windows;
using Microsoft.Win32;
using RobocopySW.Core.Models;
using RobocopySW.ViewModels;

namespace RobocopySW;

public partial class JobEditorWindow : Window
{
    private readonly JobEditorViewModel _vm;

    public JobEditorWindow(BackupJob job, IEnumerable<CredentialEntry> credentials)
    {
        InitializeComponent();
        _vm = new JobEditorViewModel(job, credentials);
        DataContext = _vm;
    }

    private void OnBrowseSource(object sender, RoutedEventArgs e)
    {
        var path = BrowseFolder(_vm.Source);
        if (path is not null) _vm.Source = path;
    }

    private void OnBrowseDest(object sender, RoutedEventArgs e)
    {
        var path = BrowseFolder(_vm.Destination);
        if (path is not null) _vm.Destination = path;
    }

    private static string? BrowseFolder(string? initial)
    {
        var dlg = new OpenFolderDialog { Title = "Seleziona cartella" };
        if (!string.IsNullOrWhiteSpace(initial) && Directory.Exists(initial))
            dlg.InitialDirectory = initial;
        return dlg.ShowDialog() == true ? dlg.FolderName : null;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var error = _vm.Validate();
        if (error is not null)
        {
            MessageBox.Show(error, "Dati mancanti", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
