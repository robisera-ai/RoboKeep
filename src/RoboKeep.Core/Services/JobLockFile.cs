namespace RoboKeep.Core.Services;

/// <summary>
/// File di lock per i job in esecuzione: viene creato all'avvio del run e cancellato nel
/// finally. Se il processo viene terminato brutalmente (crash, kill, interruzione di corrente)
/// il finally non gira e il file rimane, segnalando all'avvio successivo che quel job era
/// in corso all'ultima chiusura.
/// </summary>
public static class JobLockFile
{
    /// <summary>
    /// Crea il file di lock per <paramref name="jobName"/> in <paramref name="folder"/>
    /// e restituisce un handle che lo elimina al Dispose.
    /// </summary>
    public static IDisposable Acquire(string folder, string jobName)
    {
        Directory.CreateDirectory(folder);
        var path = LockPath(folder, jobName);
        File.WriteAllText(path, "");
        return new Handle(path);
    }

    /// <summary>
    /// Restituisce i nomi dei job per cui esiste ancora un file di lock
    /// (run non terminato pulitamente nell'ultima sessione).
    /// Non lancia eccezioni se la cartella non esiste.
    /// </summary>
    public static HashSet<string> GetInterrupted(string folder, IEnumerable<string> jobNames)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(folder)) return result;
        foreach (var name in jobNames)
        {
            if (File.Exists(LockPath(folder, name)))
                result.Add(name);
        }
        return result;
    }

    private static string LockPath(string folder, string jobName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = string.Concat(jobName.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c));
        return Path.Combine(folder, safe + ".lock");
    }

    private sealed class Handle : IDisposable
    {
        private readonly string _path;
        public Handle(string path) => _path = path;
        public void Dispose() { try { File.Delete(_path); } catch { } }
    }
}
