namespace RoboKeep.Core.Services;

/// <summary>Decide se la verifica integrità automatica è dovuta (funzione pura: il tempo è un parametro).</summary>
public static class VerifySchedule
{
    /// <summary>true se va eseguita: intervallo 0 (= a ogni backup), nessuna verifica precedente,
    /// oppure trascorsi almeno <paramref name="everyDays"/> giorni di CALENDARIO dall'ultima.
    /// Si confrontano le date, non le ore: con un backup serale alla stessa ora, la verifica di
    /// sette giorni prima è finita qualche minuto "dopo" l'ora di adesso, e un confronto al
    /// minuto la farebbe slittare ogni volta all'ottavo giorno.</summary>
    public static bool IsDue(int everyDays, DateTime? lastVerify, DateTime now)
    {
        if (everyDays <= 0 || lastVerify is null) return true;
        return (now.Date - lastVerify.Value.Date).Days >= everyDays;
    }

    /// <summary>Giorni che mancano alla prossima verifica (0 se è dovuta).</summary>
    public static int DaysUntilDue(int everyDays, DateTime? lastVerify, DateTime now) =>
        IsDue(everyDays, lastVerify, now) ? 0 : everyDays - (now.Date - lastVerify!.Value.Date).Days;
}
