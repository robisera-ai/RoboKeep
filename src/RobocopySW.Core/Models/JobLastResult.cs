namespace RobocopySW.Core.Models;

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
}
