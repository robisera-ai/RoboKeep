using System.Text.Json;
using System.Text.Json.Serialization;

namespace RoboKeep.Core.Services;

/// <summary>
/// Il biglietto da visita di una versione: quando e' nata e che cosa quel backup ha
/// cambiato. I tre elenchi sono percorsi relativi a <c>current\</c>.
/// <list type="bullet">
/// <item><see cref="Changed"/>: file che il backup ha sostituito. La copia PRECEDENTE sta nella
/// cartella della versione, allo stesso percorso relativo.</item>
/// <item><see cref="Deleted"/>: file e cartelle che il backup ha tolto da <c>current</c> perche'
/// spariti dalla sorgente. Anche questi stanno nella cartella della versione, e per molti di loro
/// quella E' l'unica copia rimasta.</item>
/// <item><see cref="Added"/>: file che il backup ha aggiunto. Non stanno nella cartella della
/// versione (prima non esistevano): servono a sapere che a quella data NON c'erano ancora.</item>
/// </list>
/// <para>Senza il manifest una cartella-versione direbbe solo "questi file esistevano prima";
/// con il manifest si puo' ricostruire l'albero completo a una data (il ripristino guidato).</para>
/// <para><b>Sta FUORI dalla cartella della versione</b>, come file gemello
/// (<c>versions\2026-09-27_213000.manifest.json</c> accanto a
/// <c>versions\2026-09-27_213000\</c>): dentro, un file dell'utente che si chiamasse
/// <c>_manifest.json</c> nella radice dell'albero ci finirebbe sopra — e quello messo da parte
/// potrebbe essere l'unica copia rimasta. Fuori, nessun percorso dell'utente puo' collidere. Il
/// gemello viene rinominato insieme alla cartella e cancellato insieme a lei.</para>
/// </summary>
public sealed class VersionManifest
{
    /// <summary>Suffisso del file gemello di una cartella-versione.</summary>
    public const string FileSuffix = ".manifest.json";

    public DateTime CreatedAt { get; set; }
    public List<string> Changed { get; set; } = new();
    public List<string> Deleted { get; set; } = new();
    public List<string> Added { get; set; } = new();

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Percorso del manifest gemello della cartella-versione indicata.</summary>
    public static string PathFor(string versionDir) => versionDir.TrimEnd('\\', '/') + FileSuffix;

    /// <summary>Scrive (o riscrive) il manifest accanto alla cartella della versione.</summary>
    public void WriteTo(string versionDir)
        => File.WriteAllText(PathFor(versionDir), JsonSerializer.Serialize(this, Options));

    /// <summary>Rilegge il manifest di una versione, o null se manca o e' illeggibile: una versione
    /// senza manifest resta comunque una cartella di file apribile con Esplora risorse.</summary>
    public static VersionManifest? ReadFrom(string versionDir)
    {
        try
        {
            var path = PathFor(versionDir);
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<VersionManifest>(File.ReadAllText(path), Options);
        }
        catch { return null; }
    }

    /// <summary>Sposta il manifest insieme alla sua cartella-versione (promozione da
    /// <c>.inprogress</c> al nome definitivo). Best-effort: una versione con i file e senza
    /// manifest resta utile, una cartella orfana di file no.</summary>
    public static void MoveWith(string fromVersionDir, string toVersionDir)
    {
        try
        {
            var from = PathFor(fromVersionDir);
            if (File.Exists(from)) File.Move(from, PathFor(toVersionDir), overwrite: true);
        }
        catch { /* best-effort: il manifest non vale il fallimento di una promozione */ }
    }

    /// <summary>Cancella il manifest gemello di una versione che non c'e' piu' (ritenzione, pulizia
    /// per spazio, residuo vuoto): senza questo resterebbero file orfani in <c>versions\</c>.
    /// Best-effort.</summary>
    public static void DeleteFor(string versionDir)
    {
        try
        {
            var path = PathFor(versionDir);
            if (File.Exists(path)) File.Delete(path);
        }
        catch { /* best-effort */ }
    }
}
