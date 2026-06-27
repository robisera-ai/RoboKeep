namespace RoboKeep.Core.Models;

/// <summary>Gravità di un avviso pre-avvio.</summary>
public enum PreflightSeverity { Info, Warning }

/// <summary>Avviso pre-avvio: gravità, chiave messaggio localizzabile, dettaglio testuale.</summary>
public sealed record PreflightWarning(PreflightSeverity Severity, string MessageKey, string Detail);

/// <summary>
/// Dati raccolti (IO) per la valutazione pre-avvio. <see cref="SourceSizeBytes"/> è null
/// quando la scansione della sorgente non è stata fatta o ha superato il budget di tempo.
/// </summary>
public sealed record PreflightInputs(
    bool DestinationReachable,
    long FreeBytes,
    long MinFreeBytes,
    long? SourceSizeBytes);
