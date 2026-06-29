using System.IO;
using System.Windows;
using Microsoft.Win32;
using RoboKeep.Core.Models;
using RoboKeep.Localization;
using RoboKeep.ViewModels;

namespace RoboKeep;

public partial class JobEditorWindow : Wpf.Ui.Controls.FluentWindow
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
        var dlg = new OpenFolderDialog { Title = Loc.Instance["Editor_BrowseTitle"] };
        if (!string.IsNullOrWhiteSpace(initial) && Directory.Exists(initial))
            dlg.InitialDirectory = initial;
        return dlg.ShowDialog() == true ? dlg.FolderName : null;
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        var error = _vm.Validate();
        if (error is not null)
        {
            MessageBox.Show(error, Loc.Instance["Common_MissingData"], MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Controllo idoneita' destinazione fuori dal thread UI (su UNC irraggiungibile
        // Directory.Exists puo' bloccare 20-30s): Task.Run con timeout, niente freeze.
        if (_vm.Versioned)
        {
            var dest = _vm.Destination;
            bool supported;
            try
            {
                supported = await System.Threading.Tasks.Task.Run(
                    () => RoboKeep.Core.Services.HardLinkSupport.IsSupported(dest))
                    .WaitAsync(System.TimeSpan.FromSeconds(5));
            }
            catch (System.TimeoutException) { supported = false; }
            if (!supported)
            {
                MessageBox.Show(Loc.Instance["Ver_DestNotSupported"],
                    Loc.Instance["Common_MissingData"], MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
