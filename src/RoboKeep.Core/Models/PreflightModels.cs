namespace RoboKeep.Core.Models;

/// <summary>Gravità di un avviso pre-avvio.</summary>
public enum PreflightSeverity { Info, Warning }

/// <summary>Avviso pre-avvio: gravità, chiave messaggio localizzabile, dettaglio testuale.</summary>
public sealed record PreflightWarning(PreflightSeverity Severity, string MessageKey, string Detail);

/// <summary>
/// Dati raccolti (IO) per la valutazione pre-avvio. <see cref="SourceSizeBytes"/> è null
/// quando la scansione della sorgente non è stata fatta o ha superato il budget di tempo.
/// <see cref="VersionedDestSupportsHardLinks"/> è null quando il controllo non è pertinente
/// (job non versionato o destinazione non raggiungibile).
/// <see cref="VssSourceEligible"/> è null quando il controllo non è pertinente (job senza VSS).
/// <see cref="DestinationDiskEvents"/> e <see cref="SourceDiskEvents"/>: errori disco recenti nel
/// registro eventi di Windows (null = non controllati).
/// </summary>
public sealed record PreflightInputs(
    bool DestinationReachable,
    long FreeBytes,
    long MinFreeBytes,
    long? SourceSizeBytes,
    bool? VersionedDestSupportsHardLinks = null,
    bool? VssSourceEligible = null,
    Services.DiskEventSummary? DestinationDiskEvents = null,
    Services.DiskEventSummary? SourceDiskEvents = null);
