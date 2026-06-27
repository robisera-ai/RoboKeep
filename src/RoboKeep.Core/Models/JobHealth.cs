namespace RoboKeep.Core.Models;

/// <summary>Stato di salute di un job dal punto di vista dell'affidabilità del backup.</summary>
public enum BackupHealth
{
    /// <summary>Ultimo backup riuscito e recente.</summary>
    Ok,
    /// <summary>Ultimo backup terminato in errore.</summary>
    Failed,
    /// <summary>Ultimo successo troppo vecchio (oltre la soglia di giorni).</summary>
    Stale,
    /// <summary>Nessun esito registrato: job mai eseguito.</summary>
    NeverRun,
}

/// <summary>Salute di un job (nome + stato).</summary>
public sealed record JobHealth(string JobName, BackupHealth Health);
