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
    /// quello atteso. Non è né un successo né un fallimento: è un terzo esito.
    /// <para>Chi legge un risultato NON deve dedurre l'esito dal solo <see cref="Success"/>:
    /// un job saltato ha <c>Success = false</c> (non ha copiato nulla) ma non va mai mostrato
    /// come errore né conteggiato come fallimento — controllare sempre prima <c>Skipped</c>.
    /// La voce di cronologia corrispondente usa invece <c>Success = true</c> proprio per non
    /// comparire in rosso: sono due domande diverse ("ha copiato?" contro "c'è un problema?").</para></summary>
    public bool Skipped { get; set; }

    /// <summary>true se il job è stato INTERROTTO perché un disco (o il suo collegamento) ha
    /// segnalato un errore hardware (CRC, settore non trovato, errore del dispositivo I/O).
    /// È sempre un fallimento (<c>Success = false</c>): serve a distinguerlo da un normale
    /// errore di copia, perché qui la cosa giusta NON è riprovare ma controllare il supporto.</summary>
    public bool HardwareError { get; set; }

    /// <summary>Dettaglio dell'errore hardware (codice, azione e percorso), se <see cref="HardwareError"/>.</summary>
    public string? HardwareErrorDetail { get; set; }

    /// <summary>true se il job non è partito perché il suo disco è a riposo: è sempre anche un
    /// <see cref="HardwareError"/>, ma il racconto cambia — non c'è nulla di INTERROTTO, il lavoro
    /// non è nemmeno iniziato, e <see cref="HardwareErrorDetail"/> è già la frase completa.</summary>
    public bool NotStarted { get; set; }

    /// <summary>Avvisi di salute del disco (registro eventi) emessi per questo run: finiscono
    /// anche nell'email, non solo nel log.</summary>
    public List<string> HealthWarnings { get; } = new();

    /// <summary>Avviso "thread limitati perché è coinvolto un disco meccanico", se il tetto è
    /// scattato. Viene ripetuto nel riepilogo finale: in cima al log finirebbe sepolto sotto
    /// migliaia di righe di robocopy.</summary>
    public string? ThreadCapNote { get; set; }

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
