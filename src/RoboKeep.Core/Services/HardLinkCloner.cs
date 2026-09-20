namespace RoboKeep.Core.Services;

/// <summary>
/// Clona ricorsivamente una cartella: ricrea le sottocartelle come reali e collega i file
/// con hard-link (zero copia di dati). Sorgente e destinazione devono stare sullo stesso volume NTFS.
/// </summary>
public static class HardLinkCloner
{
    // Un solo file non collegabile nel vecchio snapshot (bloccato, permessi, nome in conflitto) non
    // deve far crollare l'intero backup: lo si salta e si prosegue. I file saltati non vengono
    // collegati, ma robocopy — che gira subito dopo sullo snapshot — li ricopia freschi dalla
    // sorgente. Fanno eccezione gli errori HARDWARE del disco: quelli interrompono tutto (vedi Skip).
    private const int MaxReported = 10; // oltre questa soglia si contano soltanto, per non intasare il log

    /// <summary>Clona <paramref name="sourceDir"/> in <paramref name="destDir"/> via hard-link.
    /// <paramref name="onSkip"/> viene invocato per ogni elemento saltato. Restituisce il numero di
    /// elementi saltati. Lancia <see cref="DiskHardwareException"/> al primo errore hardware.</summary>
    public static int Clone(string sourceDir, string destDir, System.Action<string>? onSkip = null)
    {
        Directory.CreateDirectory(destDir);
        var state = new State { OnSkip = onSkip };
        Walk(sourceDir, sourceDir, destDir, state);
        return state.Skipped;
    }

    private sealed class State
    {
        public System.Action<string>? OnSkip;
        public int Skipped;
        public int Reported;
    }

    private static void Skip(State s, string path, System.Exception ex)
    {
        // Eccezione alla regola "salta e prosegui": un errore HARDWARE (CRC, settore non trovato,
        // errore del dispositivo) non si aggira. Creare un hard-link non legge i dati del file:
        // se fallisce cosi', e' illeggibile la zona dei metadati NTFS, e proseguire vorrebbe dire
        // far scrivere a robocopy un intero backup su un disco che sta cedendo.
        if (DiskError.IsUnreadable(ex))
            throw new DiskHardwareException(path, ex);

        s.Skipped++;
        if (s.Reported >= MaxReported) return;
        s.Reported++;
        s.OnSkip?.Invoke(path);
    }

    private static void Walk(string root, string dir, string destRoot, State s)
    {
        string[] files;
        try { files = Directory.GetFiles(dir); }
        catch (System.Exception ex) { Skip(s, dir, ex); files = System.Array.Empty<string>(); }

        foreach (var file in files)
        {
            try
            {
                var target = Path.Combine(destRoot, Path.GetRelativePath(root, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                HardLink.Create(target, file);
            }
            catch (System.Exception ex) { Skip(s, file, ex); }
        }

        string[] subs;
        try { subs = Directory.GetDirectories(dir); }
        catch (System.Exception ex) { Skip(s, dir, ex); subs = System.Array.Empty<string>(); }

        foreach (var sub in subs)
        {
            try { Directory.CreateDirectory(Path.Combine(destRoot, Path.GetRelativePath(root, sub))); }
            catch (System.Exception ex) { Skip(s, sub, ex); continue; }
            Walk(root, sub, destRoot, s);
        }
    }
}
