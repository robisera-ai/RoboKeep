using System.Globalization;

namespace RoboKeep.Core.Services;

/// <summary>Convenzioni di naming delle versioni datate (cartelle in <c>versions</c>).</summary>
public static class SnapshotName
{
    public const string Format = "yyyy-MM-dd_HHmmss";
    public const string InProgressSuffix = ".inprogress";

    public static string For(DateTime now) => now.ToString(Format, CultureInfo.InvariantCulture);

    public static bool IsInProgress(string name)
        => name.EndsWith(InProgressSuffix, StringComparison.OrdinalIgnoreCase);

    public static bool TryParse(string name, out DateTime date)
        => DateTime.TryParseExact(name, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    /// <summary>Nomi delle versioni valide (niente .inprogress, nome parsabile) in una
    /// cartella, in ordine cronologico inverso (più recente prima). Cartella mancante → vuoto.</summary>
    public static IReadOnlyList<string> ListValid(string folder)
    {
        if (!Directory.Exists(folder)) return Array.Empty<string>();
        return Directory.GetDirectories(folder)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(n => !IsInProgress(n) && TryParse(n, out _))
            .OrderByDescending(n => { TryParse(n, out var d); return d; })
            .ToList();
    }

    /// <summary>
    /// Il nome desiderato se è libero, altrimenti il primo libero avanzando di un secondo alla
    /// volta. Serve quando due run cadono nello stesso secondo (o quando si recupera una versione
    /// interrotta il cui nome è nel frattempo stato preso).
    /// <para>Avanzare di un secondo, invece di appiccicare un suffisso casuale, tiene il nome
    /// <b>interpretabile come data</b>: <see cref="TryParse"/>, <see cref="ListValid"/>, la
    /// ritenzione e l'ordinamento cronologico continuano a funzionare, mentre un
    /// <c>2026-09-27_213000_a1b2c3d4</c> sarebbe una cartella che nessuna di quelle regole vede
    /// più — invisibile alla ritenzione, quindi eterna, e invisibile all'elenco delle versioni,
    /// quindi irraggiungibile dall'utente.</para>
    /// </summary>
    /// <param name="preferred">Nome desiderato (deve essere un nome-data).</param>
    /// <param name="isTaken">true se quel nome è già occupato. Le versioni considerano
    /// occupato anche un nome che ha solo il manifest gemello.</param>
    /// <param name="maxTries">Quanti secondi provare prima di arrendersi a un suffisso univoco.</param>
    public static string FreeName(string preferred, Func<string, bool> isTaken, int maxTries = 120)
    {
        ArgumentNullException.ThrowIfNull(isTaken);
        if (!isTaken(preferred)) return preferred;

        if (TryParse(preferred, out var date))
        {
            for (var k = 1; k <= maxTries; k++)
            {
                var candidate = For(date.AddSeconds(k));
                if (!isTaken(candidate)) return candidate;
            }
        }

        // Due minuti di nomi tutti occupati (o un nome che non è una data): non si sovrascrive
        // niente, si ripiega su un suffisso univoco e la cartella resterà da guardare a mano.
        return preferred + "_" + Guid.NewGuid().ToString("N")[..8];
    }
}
