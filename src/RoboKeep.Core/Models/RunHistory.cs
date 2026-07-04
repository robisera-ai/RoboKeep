namespace RoboKeep.Core.Models;

/// <summary>Frequenza della pianificazione per-job.</summary>
public enum ScheduleKind { None, Daily, Weekly, Monthly }

/// <summary>
/// Voce della cronologia esecuzioni: un backup o una verifica di integrità.
/// Per le verifiche i conteggi sono riusati: FilesCopied = verificati,
/// FilesFailed = differenti+mancanti, FilesSkipped = saltati (file in uso).
/// </summary>
public sealed record RunHistoryEntry(
    string JobName,
    string Kind, // "backup" | "verify"
    DateTime StartedAt,
    DateTime FinishedAt,
    bool Success,
    int ExitCode,
    long FilesCopied,
    long FilesSkipped,
    long FilesExtra,
    long FilesFailed,
    long DirsFailed,
    string? LogPath);
