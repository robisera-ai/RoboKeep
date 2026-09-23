using System.Windows;
using RoboKeep.ViewModels;

namespace RoboKeep;

/// <summary>
/// Finestra «Salute dei dischi»: una scheda per disco fisico con il verdetto e i valori SMART
/// che contano. La lettura parte da sola all'apertura e si ripete con Aggiorna (per i dischi
/// SATA e USB ogni lettura chiede l'autorizzazione di amministratore).
/// </summary>
public partial class DiskHealthWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly DiskHealthViewModel _vm;

    public DiskHealthWindow(AppHost host)
    {
        InitializeComponent();
        _vm = new DiskHealthViewModel(host);
        DataContext = _vm;
        // Aprire la finestra e' gia' la richiesta di leggere: non serve premere Aggiorna.
        Loaded += async (_, _) => await _vm.RefreshAsync();
    }

    private async void OnRefresh(object sender, RoutedEventArgs e) => await _vm.RefreshAsync();
}
