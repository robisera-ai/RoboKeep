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
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return false;

            var id = Guid.NewGuid().ToString("N");
            var probe = Path.Combine(directory, $".robokeep-probe-{id}");
            var link = Path.Combine(directory, $".robokeep-link-{id}");
            File.WriteAllText(probe, "x");
            try
            {
                return HardLink.TryCreate(link, probe);
            }
            finally
            {
                try { File.Delete(link); } catch { /* best-effort */ }
                try { File.Delete(probe); } catch { /* best-effort */ }
            }
        }
        catch
        {
            return false;
        }
    }
}
