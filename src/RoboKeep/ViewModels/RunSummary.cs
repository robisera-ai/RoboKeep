using RoboKeep.Localization;

namespace RoboKeep.ViewModels;

/// <summary>
/// Riassunto di un gruppo di job per la notifica di fine esecuzione.
///
/// Le notifiche di Windows che usiamo sono «balloon tip» dell'icona nell'area di notifica, e
/// Windows ne mostra una alla volta per icona: con sei job partivano sei banner in fila, cinque
/// secondi l'uno, e restavano sei voci da chiudere a mano nel Centro notifiche. La durata non si
/// puo' imporre dal programma (il parametro e' deprecato da Vista: decide l'impostazione di
/// accessibilita' dell'utente), quindi la coda si toglie alla radice, notificando una volta sola.
/// I job falliti continuano ad avere la loro notifica, col nome: su quelli si vuole sapere subito
/// quale.
///
/// I nomi valgono piu' di un numero, ma non ci stanno sempre: si elencano finche' sono pochi,
/// poi si dice quanti restano. Una notifica che sembra dire tutto e invece taglia proprio il
/// nome che cerchi sarebbe peggio di un conteggio onesto.
/// </summary>
public static class RunSummary
{
    /// <summary>Quanti nomi si elencano prima di passare a «e altri N».</summary>
    public const int MaxNames = 3;

    /// <summary>Quanto testo accetta una notifica di Windows: <c>NOTIFYICONDATA.szInfo</c> e' di
    /// 256 caratteri col terminatore. Oltre, Windows taglia dove capita.</summary>
    public const int MaxLength = 255;

    /// <summary>true se c'e' qualcosa da riassumere. Un gruppo in cui tutti i job sono stati
    /// saltati (disco non collegato) non e' un esito: non notificava prima e non notifica ora.</summary>
    public static bool Worth(int ok, int failed) => ok + failed > 0;

    /// <summary>Chiave del titolo, diversa se almeno un job e' fallito.</summary>
    public static string TitleKey(int failed) => failed > 0 ? "Toast_BatchTitleErr" : "Toast_BatchTitleOk";

    /// <summary>Corpo della notifica: i nomi se ci stanno, i conteggi se i nomi sono troppo
    /// lunghi per il limite di Windows.</summary>
    public static string Body(IReadOnlyList<string> okNames, IReadOnlyList<string> failedNames, int skipped)
    {
        var body = Compose(okNames, failedNames, skipped);
        return body.Length <= MaxLength ? body : Counts(okNames.Count, failedNames.Count, skipped);
    }

    private static string Compose(IReadOnlyList<string> ok, IReadOnlyList<string> failed, int skipped)
    {
        var lines = new List<string>();
        if (failed.Count > 0)
        {
            // Con dei falliti i nomi che contano sono i loro, su una riga sola: quelli riusciti
            // restano un numero, e il nome di ciascun fallito e' gia' arrivato per conto suo.
            var key = failed.Count == 1 ? "Toast_BatchFailedOne" : "Toast_BatchFailedMany";
            lines.Add(string.Format(Loc.Instance[key], JoinNames(failed)));
            lines.Add(string.Format(Loc.Instance["Toast_BatchOk"], ok.Count));
        }
        else
        {
            lines.AddRange(ok.Take(MaxNames));
            if (ok.Count > MaxNames)
                lines.Add(string.Format(Loc.Instance["Toast_BatchMore"], ok.Count - MaxNames));
        }
        if (skipped > 0)
            lines.Add(string.Format(Loc.Instance["Toast_BatchSkipped"], skipped));
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>Nomi separati da virgola, al massimo <see cref="MaxNames"/>, poi «e altri N».</summary>
    private static string JoinNames(IReadOnlyList<string> names)
    {
        var shown = string.Join(", ", names.Take(MaxNames));
        if (names.Count <= MaxNames) return shown;
        return $"{shown}, {string.Format(Loc.Instance["Toast_BatchMore"], names.Count - MaxNames)}";
    }

    /// <summary>Ripiego senza nomi, su una riga. I conteggi sono scritti come «Completati: 6» e
    /// non «6 completati» perche' cosi' la frase non deve concordare al plurale in cinque lingue.</summary>
    private static string Counts(int ok, int failed, int skipped)
    {
        var parts = new List<string> { string.Format(Loc.Instance["Toast_BatchOk"], ok) };
        if (failed > 0) parts.Add(string.Format(Loc.Instance["Toast_BatchFailed"], failed));
        if (skipped > 0) parts.Add(string.Format(Loc.Instance["Toast_BatchSkipped"], skipped));
        return string.Join(" · ", parts);
    }
}
