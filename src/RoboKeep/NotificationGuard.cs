namespace RoboKeep;

/// <summary>
/// Le notifiche di Windows sono un di piu'. Windows puo' rifiutarle (icona del tray persa dopo un
/// riavvio di Esplora risorse, notifiche disattivate, area di notifica non pronta) e H.NotifyIcon
/// allora lancia un'eccezione («Show notification failed.»): presa dove capita diventerebbe
/// l'errore di un backup appena riuscito, o farebbe cadere l'applicazione. Qui si prova e basta.
/// </summary>
public static class NotificationGuard
{
    /// <summary>Esegue <paramref name="show"/>; false se la notifica non e' partita.</summary>
    public static bool TryShow(Action show)
    {
        try
        {
            show();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
