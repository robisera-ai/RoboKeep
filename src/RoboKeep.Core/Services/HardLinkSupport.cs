namespace RoboKeep.Core.Services;

/// <summary>
/// Rileva se una cartella di destinazione supporta gli hard-link, con un test funzionale
/// (crea un file di prova, prova a linkarlo, pulisce). Più affidabile del solo controllo NTFS.
/// </summary>
public static class HardLinkSupport
{
    public static bool IsSupported(string directory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directory)) return false;

            // Se la cartella non esiste ancora, risali al primo antenato esistente:
            // il supporto hard-link e' una proprieta' del volume.
            var dir = directory;
            while (!Directory.Exists(dir))
            {
                var parent = Path.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(parent) || string.Equals(parent, dir, StringComparison.OrdinalIgnoreCase))
                    return false; // nessun antenato esistente
                dir = parent;
            }

            var id = Guid.NewGuid().ToString("N");
            var probe = Path.Combine(dir, $".robokeep-probe-{id}");
            var link = Path.Combine(dir, $".robokeep-link-{id}");
            File.WriteAllText(probe, "x");
            try { return HardLink.TryCreate(link, probe); }
            finally
            {
                try { File.Delete(link); } catch { }
                try { File.Delete(probe); } catch { }
            }
        }
        catch { return false; }
    }
}
