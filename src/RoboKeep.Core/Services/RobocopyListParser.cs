namespace RoboKeep.Core.Services;

/// <summary>Che cosa il mirror farebbe a una voce elencata dall'anteprima di robocopy.</summary>
public enum ChangeKind
{
    /// <summary>File che in destinazione non c'e': verra' creato.</summary>
    NewFile,
    /// <summary>File presente in destinazione che verra' riscritto (piu' recente, piu' vecchio,
    /// cambiato, ritoccato).</summary>
    Overwrite,
    /// <summary>File presente solo in destinazione: il mirror lo rimuoverebbe.</summary>
    ExtraFile,
    /// <summary>Cartella che in destinazione non c'e': verra' creata.</summary>
    NewDir,
    /// <summary>Cartella presente solo in destinazione: il mirror la rimuoverebbe.</summary>
    ExtraDir,
}

/// <summary>Una voce dell'anteprima: che cosa succede e a quale percorso, relativo alla radice
/// (sorgente per le voci da copiare, destinazione per quelle "extra").</summary>
public sealed record ListedChange(ChangeKind Kind, string RelativePath);

/// <summary>
/// Legge l'elenco che robocopy stampa in anteprima (<c>/L /FP /BYTES</c>) e lo trasforma in voci
/// (che cosa, percorso relativo). Serve al modello di versioni per differenza: prima del mirror
/// bisogna sapere quali file di <c>current\</c> verranno sostituiti o rimossi, per metterli da parte.
/// <para><b>Le etichette di robocopy sono TRADOTTE</b> (su questa macchina, Windows in italiano,
/// stampa «Nuovo file», «Modificato», «*File supplementare», «Nuova directory», «*directory
/// supplementare»): un riconoscimento basato sulle parole inglesi non funzionerebbe. Quindi qui si
/// legge quel che la lingua non cambia:</para>
/// <list type="bullet">
/// <item>il percorso e' sempre l'ultima colonna, separata da tabulazioni, e finisce con una barra
/// rovesciata se e' una cartella;</item>
/// <item>le voci "extra" cominciano con <c>*</c> e portano il percorso di DESTINAZIONE, le altre
/// quello di SORGENTE: basta togliere la radice giusta per avere il percorso relativo;</item>
/// <item>una cartella gia' presente in entrambe le parti non ha etichetta, solo il conteggio dei
/// file: la distingue da una cartella nuova la presenza di una lettera nella colonna.</item>
/// </list>
/// <para>Resta un solo caso che le parole deciderebbero: file NUOVO contro file da SOVRASCRIVERE.
/// Le parole note (le cinque lingue di RoboKeep piu' l'inglese) vengono riconosciute, e per una
/// lingua sconosciuta si risponde <see cref="ChangeKind.Overwrite"/>; chi usa questo elenco non
/// deve fidarsene e deve guardare il disco (il file in destinazione c'e' o no?), che e' sempre la
/// risposta vera. Funzione pura: nessun accesso al disco, nessun processo.</para>
/// </summary>
public static class RobocopyListParser
{
    // "Nuovo" nelle lingue in cui Windows stampa l'output: en "New File", it "Nuovo file",
    // es "Nuevo arch.", fr "Nouveau fichier", de "Neue Datei". Il confronto e' su PAROLA INTERA,
    // non per prefisso: "Newer" (file piu' recente, da sovrascrivere) comincia per "new" e un
    // confronto per prefisso lo prenderebbe per un file nuovo, perdendo la versione precedente.
    private static readonly HashSet<string> NewWords = new(StringComparer.Ordinal)
    {
        "new", "nuovo", "nuova", "nuevo", "nueva", "nouveau", "nouvelle", "neue", "neues", "neuer",
    };

    // Righe che non descrivono un cambiamento: i file identici ("same", compaiono solo con /V) e i
    // "lonely" (extra che robocopy segnala ma non rimuove, con /XX). Riconosciute in inglese;
    // l'anteprima del versioning gira senza /V proprio per non doverci contare.
    private static readonly string[] IgnoredWords = { "same", "lonely" };

    /// <param name="lines">Le righe dell'output di robocopy, cosi' come sono state catturate.</param>
    /// <param name="sourceRoot">Radice della sorgente passata a robocopy (lo snapshot VSS, se c'era).</param>
    /// <param name="destRoot">Radice della destinazione passata a robocopy (la cartella <c>current</c>).</param>
    public static IReadOnlyList<ListedChange> Parse(IEnumerable<string> lines, string sourceRoot, string destRoot)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var src = NormalizeRoot(sourceRoot);
        var dst = NormalizeRoot(destRoot);
        var result = new List<ListedChange>();

        foreach (var raw in lines)
        {
            var line = (raw ?? "").TrimEnd('\r', '\n');
            // Le righe dell'elenco cominciano sempre con una tabulazione e hanno almeno tre colonne
            // (vuota, classe+dimensione, percorso). Intestazione, opzioni e riepilogo non ne hanno.
            if (line.Length < 2 || line[0] != '\t') continue;
            var cols = line.Split('\t');
            if (cols.Length < 3) continue;

            var path = cols[^1].TrimEnd();
            if (path.Length == 0) continue;
            // Le colonne di mezzo (classe e dimensione) stanno insieme per le cartelle e separate
            // per i file: unirle rende il resto indipendente da quale dei due formati e' arrivato.
            var meta = string.Join(' ', cols[1..^1]).Trim();

            // Righe che non chiedono niente: si scartano prima di tutto il resto, perche' un
            // "*Lonely File" comincia per * come un extra ma il mirror non lo rimuove.
            if (IsIgnored(meta)) continue;

            var isDir = path.EndsWith('\\') || path.EndsWith('/');
            var isExtra = meta.StartsWith('*');

            // Una voce "extra" sta in destinazione, tutte le altre in sorgente. Se il percorso non
            // ricade sotto la radice attesa si prova l'altra (destinazione dentro la sorgente, o
            // percorsi riscritti): se non ricade sotto nessuna delle due, la riga non ci riguarda.
            var rel = Relative(path, isExtra ? dst : src) ?? Relative(path, isExtra ? src : dst);
            if (string.IsNullOrEmpty(rel)) continue; // riga della radice stessa, o di un'altra radice

            if (isDir)
            {
                if (isExtra) result.Add(new ListedChange(ChangeKind.ExtraDir, rel));
                // Nessuna lettera nella colonna = solo il conteggio dei file: cartella gia'
                // esistente da entrambe le parti, non c'e' niente da fare.
                else if (meta.Any(char.IsLetter)) result.Add(new ListedChange(ChangeKind.NewDir, rel));
                continue;
            }

            if (isExtra) { result.Add(new ListedChange(ChangeKind.ExtraFile, rel)); continue; }
            result.Add(new ListedChange(IsNew(meta) ? ChangeKind.NewFile : ChangeKind.Overwrite, rel));
        }

        return result;
    }

    /// <summary>true se l'etichetta contiene la parola "nuovo" di una lingua nota, come parola
    /// intera (l'asterisco e la punteggiatura delle abbreviazioni non contano).</summary>
    private static bool IsNew(string meta) => meta.ToLowerInvariant()
        .Split(' ', '\t', StringSplitOptions.RemoveEmptyEntries)
        .Any(t => NewWords.Contains(t.Trim('*', '.', ',', ':')));

    private static bool IsIgnored(string meta)
    {
        var lower = meta.ToLowerInvariant();
        return IgnoredWords.Any(w => lower.Contains(w, StringComparison.Ordinal));
    }

    /// <summary>Radice normalizzata e senza separatore finale, per un confronto di prefisso.
    /// <c>GetFullPath</c> normalizza senza leggere il disco; su un percorso esotico (snapshot VSS
    /// <c>\\?\GLOBALROOT\...</c>) puo' lanciare, e allora si tiene la stringa com'e'.</summary>
    private static string NormalizeRoot(string? root)
    {
        var r = (root ?? "").Trim();
        if (r.Length == 0) return "";
        try { r = Path.GetFullPath(r); } catch { /* percorso non normalizzabile: va bene com'e' */ }
        return r.TrimEnd('\\', '/');
    }

    /// <summary>Percorso relativo di <paramref name="full"/> rispetto a <paramref name="root"/>, o
    /// null se non e' sotto quella radice. Il confronto rispetta il confine di cartella: <c>C:\dati</c>
    /// non e' la radice di <c>C:\dati-vecchi</c>.</summary>
    private static string? Relative(string full, string root)
    {
        if (root.Length == 0 || !full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return null;
        if (full.Length > root.Length && full[root.Length] is not ('\\' or '/')) return null;
        return full[root.Length..].Trim('\\', '/');
    }
}
