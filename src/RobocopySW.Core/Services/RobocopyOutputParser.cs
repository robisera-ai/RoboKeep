namespace RobocopySW.Core.Services;

/// <summary>Conteggi estratti dal riepilogo finale di robocopy.</summary>
public readonly record struct RobocopyCounts(
    long DirsCopied, long FilesCopied, long FilesSkipped, long FilesFailed, long FilesExtra);

/// <summary>
/// Estrae i conteggi dal riepilogo testuale di robocopy in modo
/// <b>indipendente dalla lingua</b>: le righe "Dirs" e "File(s)" hanno sempre
/// esattamente 6 colonne intere (Totale, Copiati, Ignorati, Mismatch, FAILED, Extra),
/// mentre le righe Byte/Tempi contengono unità o ':' e vengono ignorate.
/// </summary>
public static class RobocopyOutputParser
{
    public static RobocopyCounts ParseCounts(IEnumerable<string> lines)
    {
        long[]? dirsRow = null;
        long[]? filesRow = null;

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r');
            var colon = line.IndexOf(':');
            if (colon < 0)
                continue;

            var after = line[(colon + 1)..];
            var tokens = after.Split(' ', '\t', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length != 6)
                continue;

            var nums = new long[6];
            var allInts = true;
            for (var i = 0; i < 6; i++)
            {
                if (!long.TryParse(tokens[i], out nums[i]))
                {
                    allInts = false;
                    break;
                }
            }
            if (!allInts)
                continue;

            // La prima riga numerica a 6 colonne è "Dirs", la seconda è "File(s)".
            if (dirsRow is null)
                dirsRow = nums;
            else if (filesRow is null)
            {
                filesRow = nums;
                break;
            }
        }

        // Colonne: [0]=Totale [1]=Copiati [2]=Ignorati [3]=Mismatch [4]=FAILED [5]=Extra
        return new RobocopyCounts(
            DirsCopied: dirsRow?[1] ?? 0,
            FilesCopied: filesRow?[1] ?? 0,
            FilesSkipped: filesRow?[2] ?? 0,
            FilesFailed: filesRow?[4] ?? 0,
            FilesExtra: filesRow?[5] ?? 0);
    }
}
