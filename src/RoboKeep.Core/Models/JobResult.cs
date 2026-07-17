namespace RoboKeep.Core.Models;

/// <summary>Esito dell'esecuzione di un job, con conteggi per il report riepilogo.</summary>
public sealed class JobResult
{
    public string JobName { get; set; } = "";

    /// <summary>Exit code grezzo restituito da robocopy (0–16+).</summary>
    public int ExitCode { get; set; }

    /// <summary>true se l'esito è considerato riuscito (exit code &lt; 8).</summary>
    public bool Success { get; set; }

    /// <summary>Descrizione leggibile dell'esito (da ExitCodeInterpreter).</summary>
    public string Status { get; set; } = "";

    public DateTime StartedAt { get; set; }
    public TimeSpan Duration { get; set; }

    /// <summary>Percorso del log (eventualmente .zip) prodotto.</summary>
    public string? LogPath { get; set; }

    /// <summary>true se eseguito in modalità anteprima (<c>/L</c>), senza modifiche reali.</summary>
    public bool DryRun { get; set; }

    /// <summary>true se il job non è stato eseguito perché il disco di destinazione non è
    /// quello atteso. Non è né un successo né un fallimento: è un terzo esito.</summary>
    public bool Skipped { get; set; }

    // Conteggi estratti dal riepilogo robocopy (best-effort, indipendenti dalla lingua).
    public long DirsCopied { get; set; }
    public long FilesCopied { get; set; }
    public long FilesSkipped { get; set; }

    /// <summary>File "extra": in mirror sono quelli rimossi/da rimuovere dalla destinazione.</summary>
    public long FilesExtra { get; set; }

    public long FilesFailed { get; set; }

    /// <summary>Cartelle non copiate (es. accesso negato): contribuiscono a un esito di errore.</summary>
    public long DirsFailed { get; set; }

    /// <summary>Cartelle "extra": in mirror sono quelle rimosse/da rimuovere dalla destinazione.</summary>
    public long DirsExtra { get; set; }
}
