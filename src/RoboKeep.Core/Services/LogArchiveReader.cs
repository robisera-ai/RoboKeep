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
}
