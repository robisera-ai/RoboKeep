using System.ComponentModel;
using System.Windows;
using RoboKeep.Core.Services;
using RoboKeep.Localization;
using RoboKeep.ViewModels;

namespace RoboKeep;

/// <summary>
/// L'avviso di una versione nuova. Prima era un banner nella pagina principale: chi premeva
/// «Controlla ora» nella scheda Info doveva chiudere le Impostazioni per andare a leggerlo.
/// Qui l'avviso arriva davanti a chi l'ha chiesto, compreso l'avvio.
///
/// Modale per necessita': le Impostazioni sono aperte con ShowDialog(), e una finestra non
/// modale aperta da li' verrebbe disabilitata da Windows. Un backup in corso non ne soffre,
/// perche' gira fuori dal thread dell'interfaccia.
/// </summary>
public partial class UpdateWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly MainViewModel _vm;
    private bool _downloading;

    private UpdateWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        Headline.Text = string.Format(Loc.Instance["Upd_Headline"], vm.LatestUpdateVersion);
        Detail.Text = string.Format(Loc.Instance["Upd_CurrentAndSize"],
            UpdateChecker.Current, vm.UpdatePackageSize);
        Body.Text = Loc.Instance["Upd_HowTo"];
    }

    /// <summary>Apre l'avviso davanti a <paramref name="owner"/>. Non fa niente se non c'e'
    /// nessuna versione nuova o se la finestra proprietaria non e' davvero a video: nascosta
    /// nell'area di notifica, o ridotta a icona. In quei casi l'avviso arriverebbe addosso a
    /// chi sta facendo altro, con sotto una finestra che non si vede.</summary>
    public static void ShowFor(Window owner, MainViewModel vm)
    {
        if (string.IsNullOrEmpty(vm.LatestUpdateVersion)) return;
        if (!IsOnScreen(owner)) return;
        new UpdateWindow(vm) { Owner = owner }.ShowDialog();
    }

    /// <summary>La finestra e' davanti all'utente. Attenzione: <c>IsVisible</c> resta true
    /// anche per una finestra ridotta a icona, quindi da solo non basta.</summary>
    public static bool IsOnScreen(Window w) => w.IsVisible && w.WindowState != WindowState.Minimized;

    private void OnWhatsNew(object sender, RoutedEventArgs e) => _vm.OpenUpdatePage();

    private async void OnDownload(object sender, RoutedEventArgs e)
    {
        if (_downloading) return;
        _downloading = true;
        // Mentre scarica non c'e' niente da premere: non si annulla (DownloadAsync non lo
        // prevede) e la finestra non si chiude, altrimenti resterebbe un .part senza nessuno
        // che lo finisca.
        WhatsNewButton.Visibility = Visibility.Collapsed;
        DownloadButton.Visibility = Visibility.Collapsed;
        CloseButton.Visibility = Visibility.Collapsed;
        DownloadProgress.Visibility = Visibility.Visible;
        Body.Text = string.Format(Loc.Instance["Upd_Downloading"], 0);

        var progress = new Progress<double>(p =>
        {
            DownloadProgress.Value = p * 100;
            Body.Text = string.Format(Loc.Instance["Upd_Downloading"], (int)(p * 100));
        });
        // Il view model gestisce da se' gli errori: qui arriva solo l'esito.
        var (ok, folder) = await _vm.DownloadUpdateAsync(progress);

        _downloading = false;
        DownloadProgress.Visibility = Visibility.Collapsed;
        Body.Text = ok
            ? string.Format(Loc.Instance["Upd_Downloaded"], folder)
            : Loc.Instance["Upd_DownloadFailed"];
        // Non riuscito: si puo' riprovare. Riuscito: non c'e' altro da fare qui.
        DownloadButton.Visibility = ok ? Visibility.Collapsed : Visibility.Visible;
        CloseButton.Content = Loc.Instance["Upd_Close"];
        CloseButton.Visibility = Visibility.Visible;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    // La X della barra del titolo e Esc non devono interrompere uno scaricamento in corso.
    protected override void OnClosing(CancelEventArgs e)
    {
        if (_downloading) e.Cancel = true;
        base.OnClosing(e);
    }
}
