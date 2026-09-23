namespace RoboKeep.Core.Models;

/// <summary>Ultimo esito di un job, persistito su disco per essere mostrato nella GUI.</summary>
public sealed class JobLastResult
{
    public string JobName { get; set; } = "";
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public long FilesCopied { get; set; }
    public long FilesSkipped { get; set; }
    public long FilesExtra { get; set; }
    public long FilesFailed { get; set; }
    public long DirsFailed { get; set; }
    public DateTime FinishedAt { get; set; }

    /// <summary>true se l'ultimo run è stato interrotto da un errore hardware del disco (o non è
    /// partito perché il disco era a riposo): la UI lo mostra con un'icona dedicata e il consiglio
    /// di controllare il supporto.</summary>
    public bool HardwareError { get; set; }
    public string? HardwareErrorDetail { get; set; }
}
