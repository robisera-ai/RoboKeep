using System.IO.Compression;

namespace RoboKeep.Core.Services;

/// <summary>
/// Legge il testo di un log archiviato dal suo percorso (voce di cronologia): file .log in
/// chiaro oppure .zip prodotto da LogService (prima entry). Null = log non più disponibile
/// (cancellato dalla pulizia, zip corrotto): il chiamante mostra un messaggio, non un errore.
/// </summary>
public static class LogArchiveReader
{
    public static string? ReadLogText(string? logPath)
    {
        if (string.IsNullOrWhiteSpace(logPath) || !File.Exists(logPath))
            return null;
        try
        {
            if (!logPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return File.ReadAllText(logPath);

            using var zip = ZipFile.OpenRead(logPath);
            var entry = zip.Entries.FirstOrDefault();
            if (entry is null) return null;
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd();
        }
        catch { return null; }
    }

    /// <summary>
    /// Prepara una copia in chiaro del log da aprire con un editor esterno (Blocco note): i log
    /// sono archiviati in .zip e un editor non li apre. Scrive <c>&lt;nome&gt;.log</c> in
    /// <paramref name="viewerDir"/> (UTF-8 con BOM, cosi' le accentate si leggono giuste) e
    /// restituisce il percorso; null se il log non e' piu' disponibile. Le copie dei giorni
    /// precedenti vengono rimosse: sono duplicati usa-e-getta, l'originale resta nell'archivio.
    /// </summary>
    public static string? ExtractForViewing(string? logPath, string viewerDir)
    {
        var text = ReadLogText(logPath);
        if (text is null) return null;
        try
        {
            Directory.CreateDirectory(viewerDir);
            foreach (var old in Directory.GetFiles(viewerDir, "*.log"))
            {
                try { if (File.GetLastWriteTime(old) < DateTime.Now.AddDays(-1)) File.Delete(old); }
                catch { /* aperto in un editor: lo si lascia */ }
            }

            var name = Path.GetFileName(logPath!);
            if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
            if (!name.EndsWith(".log", StringComparison.OrdinalIgnoreCase)) name += ".log";
            var target = Path.Combine(viewerDir, name);
            File.WriteAllText(target, text, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            return target;
        }
        catch { return null; }
    }
}
