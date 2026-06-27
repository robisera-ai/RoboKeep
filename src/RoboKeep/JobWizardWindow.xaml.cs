using System.IO;
using System.Windows;
using Microsoft.Win32;
using RoboKeep.Core.Models;
using RoboKeep.Localization;
using RoboKeep.ViewModels;

namespace RoboKeep;

public partial class JobWizardWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly JobWizardViewModel _vm = new();

    /// <summary>Job costruito dalle risposte (null se l'utente ha scelto "Salta e configura a mano").</summary>
    public BackupJob? ResultJob { get; private set; }

    public JobWizardWindow()
    {
        InitializeComponent();
        DataContext = _vm;
    }

    private void OnBrowseSource(object sender, RoutedEventArgs e)
    {
        var p = BrowseFolder(_vm.Source);
        if (p is not null) _vm.Source = p;
    }

    private void OnBrowseDest(object sender, RoutedEventArgs e)
    {
        var p = BrowseFolder(_vm.Destination);
        if (p is not null) _vm.Destination = p;
    }

    private static string? BrowseFolder(string? initial)
    {
        var dlg = new OpenFolderDialog { Title = Loc.Instance["Editor_BrowseTitle"] };
        if (!string.IsNullOrWhiteSpace(initial) && Directory.Exists(initial))
            dlg.InitialDirectory = initial;
        return dlg.ShowDialog() == true ? dlg.FolderName : null;
    }

    private void OnBack(object sender, RoutedEventArgs e) => _vm.GoBack();
    private void OnNext(object sender, RoutedEventArgs e) => _vm.GoNext();
    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    // Salta: apre l'editor vuoto (ResultJob = null lo segnala alla MainWindow).
    private void OnSkip(object sender, RoutedEventArgs e)
    {
        ResultJob = null;
        DialogResult = true;
    }

    private void OnOpen(object sender, RoutedEventArgs e)
    {
        ResultJob = _vm.BuildResult();
        DialogResult = true;
    }
}
