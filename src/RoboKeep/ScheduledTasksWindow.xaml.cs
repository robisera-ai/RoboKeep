using System.Windows;
using RoboKeep.Localization;
using RoboKeep.ViewModels;

namespace RoboKeep;

/// <summary>
/// Finestra «Attività pianificate di Windows»: elenca le sole attività create da RoboKeep,
/// dice a quale job appartengono e permette di eliminare quelle rimaste orfane. Non si
/// modifica niente: l'attività è generata dal job, e un ritocco fatto qui (o in Windows)
/// verrebbe sovrascritto al primo salvataggio del job.
/// </summary>
public partial class ScheduledTasksWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly ScheduledTasksViewModel _vm;

    /// <summary>true se un'eliminazione ha tolto la pianificazione a un job (l'elenco dei job
    /// della finestra principale va ricaricato).</summary>
    public bool JobsChanged => _vm.JobsChanged;

    public ScheduledTasksWindow(AppHost host)
    {
        InitializeComponent();
        _vm = new ScheduledTasksViewModel(host);
        DataContext = _vm;
        Loaded += OnLoadedRefresh;
    }

    // Aprire la finestra è già la richiesta di leggere: non serve premere Aggiorna. Il gestore
    // si sgancia subito, così una sola lettura per apertura anche se Loaded torna a scattare.
    private async void OnLoadedRefresh(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedRefresh;
        await _vm.RefreshAsync();
    }

    private async void OnRefresh(object sender, RoutedEventArgs e) => await _vm.RefreshAsync();

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ScheduledTaskRow row) return;

        // La conferma nomina l'attività: con più righe simili si deve sapere QUALE si cancella.
        // Se e' di un job, dice anche che il job restera' senza pianificazione.
        var text = row.JobName is { } jobName
            ? string.Format(Loc.Instance["Tasks_DeleteConfirmJob"], row.Name, jobName)
            : string.Format(Loc.Instance["Tasks_DeleteConfirm"], row.Name);
        var ok = MessageBox.Show(this, text,
            Loc.Instance["Common_Confirm"], MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (ok != MessageBoxResult.Yes) return;

        await _vm.DeleteAsync(row);
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
