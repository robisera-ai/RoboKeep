using System.Windows;

namespace RoboKeep;

/// <summary>Che cosa ha scelto l'utente davanti agli avvisi pre-avvio.</summary>
public enum PreflightChoice
{
    Cancel,
    Start,
    /// <summary>Avvia, e segna come controllati gli errori del registro di Windows mostrati: non
    /// verranno piu' segnalati, quelli nuovi si'.</summary>
    AcknowledgeAndStart,
}

/// <summary>
/// La conferma pre-avvio. Prima era una finestrella di Windows con «Sì» e «No»: gli errori disco
/// del registro di Windows restano li' per due settimane, e lo stesso episodio, gia' chiarito,
/// ricompariva a ogni backup. Qui c'e' anche «Ho controllato, avvia», solo quando tra gli avvisi
/// ci sono quegli errori.
/// </summary>
public partial class PreflightWindow : Wpf.Ui.Controls.FluentWindow
{
    public PreflightChoice Choice { get; private set; } = PreflightChoice.Cancel;

    public PreflightWindow(string body, bool canAcknowledge)
    {
        InitializeComponent();
        Body.Text = body;
        AckButton.Visibility = canAcknowledge ? Visibility.Visible : Visibility.Collapsed;
        AckHint.Visibility = AckButton.Visibility;
    }

    private void OnStart(object sender, RoutedEventArgs e) => Finish(PreflightChoice.Start);
    private void OnAcknowledge(object sender, RoutedEventArgs e) => Finish(PreflightChoice.AcknowledgeAndStart);
    private void OnCancel(object sender, RoutedEventArgs e) => Finish(PreflightChoice.Cancel);

    private void Finish(PreflightChoice choice)
    {
        Choice = choice;
        DialogResult = choice != PreflightChoice.Cancel;
    }
}
