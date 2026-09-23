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
    /// <summary>Il disco del job non è collegato e l'ultimo backup sarebbe altrimenti "vecchio":
    /// non è un allarme ma un'informazione neutra (un disco a riposo è normale). Oltre la rete
    /// di sicurezza dei 90 giorni torna a essere <see cref="Stale"/>.</summary>
    Waiting,
    /// <summary>Esiste un file di lock residuo: il job era in corso all'ultima chiusura anomala.</summary>
    Interrupted,
    /// <summary>Ultimo run interrotto da un errore hardware del disco (o non partito perché il
    /// disco è a riposo): non "riprova", controlla il supporto.</summary>
    HardwareError,
}

/// <summary>Salute di un job (nome + stato).</summary>
public sealed record JobHealth(string JobName, BackupHealth Health);
