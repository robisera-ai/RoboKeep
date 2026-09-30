using System.IO;
using System.Windows;
using Microsoft.Win32;
using RoboKeep.Core.Models;
using RoboKeep.Core.Services;
using RoboKeep.Localization;
using RoboKeep.ViewModels;

namespace RoboKeep;

/// <summary>
/// Finestra «Ripristina»: scegli una data, spunta che cosa recuperare e dove metterlo. Legge
/// soltanto: la destinazione del backup non viene toccata, e la sorgente del job è vietata come
/// bersaglio — un ripristino sopra i file di oggi sarebbe irreversibile e basterebbe un clic.
/// </summary>
public partial class RestoreWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly RestoreViewModel _vm;
    private readonly string? _preselect;

    /// <param name="job">Il job da cui recuperare (serve la destinazione, e la sorgente per il
    /// controllo di sicurezza).</param>
    /// <param name="preselectVersion">Nome della versione da mostrare all'apertura (arriva da
    /// «Versioni...», dove l'utente ne ha già scelta una); null = «Adesso».</param>
    public RestoreWindow(BackupJob job, string? preselectVersion = null)
    {
        InitializeComponent();
        _vm = new RestoreViewModel(job);
        _preselect = preselectVersion;
        DataContext = _vm;
        Loaded += OnLoadedRead;
    }

    // Aprire la finestra è già la richiesta di leggere. Il gestore si sgancia subito: una sola
    // lettura per apertura anche se Loaded torna a scattare.
    private async void OnLoadedRead(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedRead;
        await _vm.LoadPointsAsync(_preselect);
    }

    private void OnBrowseTarget(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = Loc.Instance["Editor_BrowseTitle"] };
        if (!string.IsNullOrWhiteSpace(_vm.TargetFolder) && Directory.Exists(_vm.TargetFolder))
            dlg.InitialDirectory = _vm.TargetFolder;
        if (dlg.ShowDialog() == true) _vm.TargetFolder = dlg.FolderName;
    }

    private async void OnRestoreSelection(object sender, RoutedEventArgs e)
        => await RunAsync(_vm.SelectedPlan);

    /// <summary>Conferma con i numeri in chiaro — quanti file, quanto spazio, in quale cartella —
    /// e poi copia. Chi conferma deve poter vedere che cosa sta confermando.</summary>
    private async Task RunAsync(RestorePlan plan)
    {
        if (plan.Files.Count == 0)
        {
            MessageBox.Show(this, Loc.Instance["Restore_NothingSelected"],
                Loc.Instance["Restore_Title"], MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var text = string.Format(Loc.Instance["Restore_Confirm"],
            plan.Files.Count, VersionsUsage.Describe(plan.TotalBytes), _vm.TargetFolder.Trim());
        if (MessageBox.Show(this, text, Loc.Instance["Common_Confirm"],
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        await _vm.RestoreAsync(plan);

        // Tutto a posto: un ultimo messaggio con l'esito (e la cartella a portata di clic), poi la
        // finestra si chiude. Con un annullamento o file non ripristinati resta aperta: l'elenco
        // di cosa è rimasto indietro sta qui sotto.
        if (!_vm.LastRestoreClean) return;
        var done = string.Format(Loc.Instance["Restore_DoneAsk"], _vm.Status, _vm.RestoredTo);
        if (MessageBox.Show(this, done, Loc.Instance["Restore_Title"],
                MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            OnOpenFolder(this, new RoutedEventArgs());
        Close();
    }

    private void OnCancelCopy(object sender, RoutedEventArgs e) => _vm.Cancel();

    private void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(_vm.RestoredTo)) return;
        // Percorso tra virgolette: un nome con uno spazio dentro arriverebbe altrimenti a
        // Esplora risorse spezzato in due argomenti.
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = "\"" + _vm.RestoredTo + "\"",
            UseShellExecute = true,
        });
    }

    // La casella dell'intestazione: tutto spuntato -> niente; altrimenti (niente o «a meta'») -> tutto.
    // Si decide qui e non col giro a tre stati di WPF, che da «a meta'» porterebbe a «niente».
    private void OnToggleAll(object sender, RoutedEventArgs e) => _vm.SelectAll(_vm.AllChecked != true);

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
