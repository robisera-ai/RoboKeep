namespace RoboKeep.Core.Services;

/// <summary>
/// File di lock per i job in esecuzione, con due compiti.
/// <para><b>Esclusione</b>: per tutta la durata del run il file resta aperto in modo esclusivo.
/// Un secondo run dello stesso job (la finestra e l'attivita' pianificata, o due attivita') non
/// riesce ad aprirlo e non parte: due run sulla stessa destinazione si spostano i file a vicenda e
/// uno cancellerebbe la versione in costruzione dell'altro. Un handle aperto, a differenza di un
/// mutex, si puo' rilasciare da qualunque thread: il run e' asincrono.</para>
/// <para><b>Run interrotti</b>: alla fine il file viene cancellato. Se il processo muore
/// (crash, kill, corrente) Windows chiude l'handle ma il file resta, e all'avvio successivo dice
/// che quel job era in corso.</para>
/// </summary>
public static class JobLockFile
{
    private const int ErrorSharingViolation = 32;
    private const int ErrorLockViolation = 33;

    /// <summary>
    /// Prende il lock di <paramref name="jobName"/> in <paramref name="folder"/>. null se lo
    /// tiene gia' un altro run (di questo o di un altro processo). Il Dispose lo rilascia e
    /// cancella il file.
    /// </summary>
    public static IDisposable? TryAcquire(string folder, string jobName)
    {
        Directory.CreateDirectory(folder);
        var path = LockPath(folder, jobName);
        try
        {
            var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return new Handle(path, stream);
        }
        catch (IOException ex) when (IsHeldElsewhere(ex))
        {
            return null;
        }
    }

    /// <summary>
    /// Restituisce i nomi dei job per cui esiste ancora un file di lock che nessuno tiene aperto
    /// (run non terminato pulitamente). Un job che sta girando adesso in un altro processo non e'
    /// interrotto e non compare. Non lancia eccezioni se la cartella non esiste.
    /// </summary>
    public static HashSet<string> GetInterrupted(string folder, IEnumerable<string> jobNames)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(folder)) return result;
        foreach (var name in jobNames)
        {
            var path = LockPath(folder, name);
            if (File.Exists(path) && !IsHeld(path))
                result.Add(name);
        }
        return result;
    }

    private static bool IsHeld(string path)
    {
        try
        {
            using var probe = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return false;
        }
        catch (IOException ex) when (IsHeldElsewhere(ex))
        {
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsHeldElsewhere(IOException ex) =>
        (ex.HResult & 0xFFFF) is ErrorSharingViolation or ErrorLockViolation;

    private static string LockPath(string folder, string jobName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = string.Concat(jobName.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c));
        return Path.Combine(folder, safe + ".lock");
    }

    private sealed class Handle : IDisposable
    {
        private readonly string _path;
        private FileStream? _stream;

        public Handle(string path, FileStream stream) { _path = path; _stream = stream; }

        public void Dispose()
        {
            if (_stream is null) return;
            _stream.Dispose();
            _stream = null;
            try { File.Delete(_path); } catch { /* lo tiene gia' il run successivo: va bene cosi' */ }
        }
    }
}
