namespace RoboKeep.Tests;

/// <summary>
/// Durante l'esecuzione il pulsante premuto diventa «Annulla» e tiene il fuoco della tastiera: uno
/// Spazio o un Invio dati per abitudine interrompevano un backup lungo senza che l'utente se ne
/// accorgesse. Annullare deve sempre passare da una domanda, e il menu dell'icona (che non cambia
/// scritta) non deve annullare mai.
/// </summary>
public class RunToggleTests
{
    [Fact]
    public void NothingRunning_Starts()
        => Assert.Equal(ToggleAction.Start, RunToggle.Decide(sameKindRunning: false, otherKindRunning: false, fromTray: false));

    [Fact]
    public void TheSameKindRunning_FromTheWindow_AsksBeforeCancelling()
        => Assert.Equal(ToggleAction.AskToCancel, RunToggle.Decide(sameKindRunning: true, otherKindRunning: false, fromTray: false));

    [Fact]
    public void TheSameKindRunning_FromTheTrayMenu_NeverCancels()
        => Assert.Equal(ToggleAction.Ignore, RunToggle.Decide(sameKindRunning: true, otherKindRunning: false, fromTray: true));

    [Fact]
    public void AnotherKindRunning_IsIgnored()
        => Assert.Equal(ToggleAction.Ignore, RunToggle.Decide(sameKindRunning: false, otherKindRunning: true, fromTray: false));
}
