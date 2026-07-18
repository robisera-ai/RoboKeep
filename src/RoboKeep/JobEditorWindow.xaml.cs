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
    private readonly string _originalSnapshot;

    public JobEditorWindow(BackupJob job, IEnumerable<CredentialEntry> credentials)
    {
        InitializeComponent();
        _vm = new JobEditorViewModel(job, credentials);
        DataContext = _vm;
        // Fotografia dello stato iniziale: alla chiusura la confrontiamo con lo stato finale
        // per sapere se c'e' davvero qualcosa da perdere (vedi OnClosing).
        _originalSnapshot = Snapshot();
        Closing += OnClosing;
    }

    private string Snapshot()
    {
        try { return System.Text.Json.JsonSerializer.Serialize(_vm.Job); }
        catch { return ""; } // in caso di guaio non intrappoliamo l'utente nella finestra
    }

    // Avvisa prima di perdere modifiche non salvate, ma SOLO chiudendo con la X: Salva e Annulla
    // impostano DialogResult (true/false) e sono scelte esplicite, la X lo lascia null. E solo se
    // qualcosa e' davvero cambiato: un avviso che compare sempre si impara a ignorarlo.
    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DialogResult is not null) return;
        if (Snapshot() == _originalSnapshot) return;
        var r = MessageBox.Show(Loc.Instance["Editor_DiscardConfirm"], Loc.Instance["Editor_DiscardTitle"],
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes) e.Cancel = true; // "No" -> resta nell'editor
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

    private void OnUseThisDisk(object sender, RoutedEventArgs e) => _vm.UseCurrentVolume();

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
            bool? supported = null; // null = non determinato entro il tempo
            try
            {
                supported = await System.Threading.Tasks.Task.Run(
                    () => RoboKeep.Core.Services.HardLinkSupport.IsSupported(dest))
                    .WaitAsync(System.TimeSpan.FromSeconds(8));
            }
            catch (System.TimeoutException) { /* non determinato: vedi sotto */ }

            // Blocca SOLO con una risposta certa "non supportato". Un timeout (disco esterno
            // lento a rispondere allo spin-up) non deve impedire di salvare un job legittimo su
            // NTFS: al run, se davvero gli hard-link non ci sono, il versioning degrada a copia
            // normale con avviso, mai perdita di dati. "Non lo so" non e' "no".
            if (supported == false)
            {
                MessageBox.Show(Loc.Instance["Ver_DestNotSupported"],
                    Loc.Instance["Common_MissingData"], MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        // VSS richiede sorgente su volume NTFS locale: avvisa subito, non solo al run.
        // Solo informativo: il salvataggio prosegue (al run scatterà il fallback con avviso).
        if (_vm.Job.UseVss && !RoboKeep.Core.Services.VssEligibility.IsEligible(_vm.Job.Source))
        {
            MessageBox.Show(
                Loc.Instance["Preflight_VssNotEligible"],
                _vm.Job.Name,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
