namespace RoboKeep;

/// <summary>Cosa fare quando si preme un pulsante di esecuzione (o la voce del menu dell'icona).</summary>
public enum ToggleAction
{
    /// <summary>Niente in corso: si parte.</summary>
    Start,

    /// <summary>Sta girando proprio questa operazione: il pulsante e' diventato «Annulla», ma si
    /// interrompe solo dopo una conferma esplicita.</summary>
    AskToCancel,

    /// <summary>Non si fa niente: c'e' in corso un'altra operazione, oppure la richiesta arriva dal
    /// menu dell'icona, che non cambia scritta e quindi non deve mai annullare.</summary>
    Ignore,
}

/// <summary>
/// Decide l'effetto di un pulsante di esecuzione. Durante l'esecuzione il pulsante premuto diventa
/// «Annulla» e tiene il fuoco della tastiera: uno Spazio o un Invio dati per abitudine bastavano a
/// interrompere un backup lungo senza che l'utente se ne accorgesse.
/// </summary>
public static class RunToggle
{
    public static ToggleAction Decide(bool sameKindRunning, bool otherKindRunning, bool fromTray)
    {
        if (sameKindRunning) return fromTray ? ToggleAction.Ignore : ToggleAction.AskToCancel;
        if (otherKindRunning) return ToggleAction.Ignore;
        return ToggleAction.Start;
    }
}
