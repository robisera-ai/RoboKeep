using System.Text;

namespace RoboKeep.Core.Services;

/// <summary>
/// Scrittura "tutto o niente" dei file di stato (configurazione, manifest delle versioni,
/// cronologia, esiti...). Prima un file temporaneo accanto, portato fino al disco, poi uno
/// spostamento sopra quello buono: chi legge trova sempre la versione vecchia o quella nuova, mai
/// una a meta'. Un'interruzione (crash, PC spento, disco staccato) lascia al massimo il
/// temporaneo, non un JSON troncato che al prossimo avvio non si legge piu'.
/// </summary>
public static class AtomicFile
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Come <see cref="File.WriteAllText(string, string)"/> (UTF-8 senza BOM, se non
    /// indicato), ma atomico. Crea la cartella se manca.</summary>
    public static void WriteAllText(string path, string content, Encoding? encoding = null)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        // Nome unico: due processi (la finestra e un'attivita' pianificata) possono salvare lo
        // stesso file nello stesso momento, e non devono scriversi nello stesso temporaneo.
        var tmp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = (encoding ?? Utf8NoBom).GetPreamble().Concat((encoding ?? Utf8NoBom).GetBytes(content)).ToArray();
                stream.Write(bytes);
                // Fino al disco, non solo alla cache: senza, dopo un'interruzione di corrente il
                // rinomina (gia' registrato dal file system) potrebbe puntare a dati mai scritti.
                stream.Flush(flushToDisk: true);
            }
            File.Move(tmp, path, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* resta un .tmp: innocuo */ }
        }
    }
}
