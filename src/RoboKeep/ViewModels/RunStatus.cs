using RoboKeep.Localization;

namespace RoboKeep.ViewModels;

/// <summary>Formatta l'esito di un job per la colonna "Ultimo esito" (live e persistito).</summary>
public static class RunStatus
{
    public static string Format(bool success, long copied, long skipped, long extra, long failed, DateTime when)
    {
        var esito = success ? Loc.Instance["Run_OK"] : Loc.Instance["Run_Error"];
        var errori = failed > 0 ? $" · {failed} {Loc.Instance["Stat_Errors"]}" : "";
        return $"{esito} · {copied} {Loc.Instance["Stat_Copied"]} · {skipped} {Loc.Instance["Stat_Unchanged"]}" +
               $" · {extra} {Loc.Instance["Stat_Extra"]}{errori}  ({when:dd/MM HH:mm})";
    }
}
