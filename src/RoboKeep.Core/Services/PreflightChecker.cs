using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>
/// Valutazione pura dei controlli pre-avvio: produce la lista di avvisi a partire dai dati
/// già raccolti. Non blocca: gli avvisi servono a chiedere conferma all'utente.
/// </summary>
public static class PreflightChecker
{
    public static IReadOnlyList<PreflightWarning> Evaluate(PreflightInputs i)
    {
        var w = new List<PreflightWarning>();

        if (!i.DestinationReachable)
        {
            w.Add(new PreflightWarning(PreflightSeverity.Warning, "Preflight_DestUnreachable", ""));
            return w; // senza destinazione, gli altri controlli non hanno senso.
        }

        if (i.FreeBytes < i.MinFreeBytes)
            w.Add(new PreflightWarning(PreflightSeverity.Warning, "Preflight_LowSpace",
                $"{i.FreeBytes / (1024 * 1024)} MB"));

        if (i.SourceSizeBytes is long size && size > i.FreeBytes)
            w.Add(new PreflightWarning(PreflightSeverity.Warning, "Preflight_SourceBigger",
                $"{size / (1024 * 1024)} MB"));

        return w;
    }
}
