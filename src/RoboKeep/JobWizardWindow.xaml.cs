using System.IO;
using System.Windows;
using Microsoft.Win32;
using RoboKeep.Core.Models;
using RoboKeep.Localization;
using RoboKeep.ViewModels;

namespace RoboKeep;

public partial class JobWizardWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly JobWizardViewModel _vm;

    /// <summary>Credenziale nuova da creare per il job: share, utente, password in chiaro (da
    /// cifrare con DPAPI prima di salvarla). Null se non serve o l'utente ha lasciato i campi vuoti.</summary>
    public (string Share, string User, string Password)? NewCredential { get; private set; }

    /// <summary>Job costruito dalle risposte (null se l'utente ha scelto "Salta e configura a mano").</summary>
    public BackupJob? ResultJob { get; private set; }

    public JobWizardWindow(IEnumerable<CredentialEntry> credentials)
    {
        InitializeComponent();
        _vm = new JobWizardViewModel(credentials);
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
        // Credenziale nuova solo se la share la richiede e l'utente ha compilato entrambi i campi:
        // campi vuoti = "la imposto dopo", come dice il testo.
        if (_vm.ShowNetCredFields && _vm.NetworkShare is { } share
            && !string.IsNullOrWhiteSpace(_vm.NetUser) && NetPasswordBox.Password.Length > 0)
            NewCredential = (share, _vm.NetUser.Trim(), NetPasswordBox.Password);
        DialogResult = true;
    }
}
