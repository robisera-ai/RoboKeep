using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>
/// Ragiona sui percorsi UNC (<c>\\server\share\...</c>) per la creazione guidata: da un percorso
/// ricava la share, cerca una credenziale gia' salvata che la copra, propone un nome per una
/// credenziale nuova. Funzioni pure: nessun accesso alla rete.
/// </summary>
public static class NetworkShare
{
    /// <summary>La share <c>\\server\share</c> di un percorso UNC, o null se il percorso non e' UNC
    /// (le unita' mappate hanno una lettera: la credenziale la gestisce Windows, non RoboKeep).</summary>
    public static string? TryGetShare(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var p = path.Trim();
        if (!p.StartsWith(@"\\", StringComparison.Ordinal)) return null;
        var parts = p.TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;
        return @"\\" + parts[0] + @"\" + parts[1];
    }

    /// <summary>Il nome del server di una share (<c>\\nas01\backup$</c> → <c>nas01</c>).</summary>
    public static string ServerOf(string share) =>
        share.TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? share;

    /// <summary>La prima share UNC tra destinazione e sorgente (la destinazione prima: e' quella
    /// su cui si scrive), o null se nessuna delle due e' UNC.</summary>
    public static string? FirstShare(string? destination, string? source) =>
        TryGetShare(destination) ?? TryGetShare(source);

    /// <summary>Credenziale gia' salvata che copre la share: stesso host (la share intera o il solo
    /// server), senza distinzione di maiuscole. Null se non c'e'.</summary>
    public static CredentialEntry? FindCredential(IEnumerable<CredentialEntry> credentials, string share)
    {
        var server = ServerOf(share);
        return credentials.FirstOrDefault(c =>
            string.Equals(c.Host.Trim().TrimEnd('\\'), share, StringComparison.OrdinalIgnoreCase)
            || string.Equals(c.Host.Trim().TrimStart('\\').TrimEnd('\\'), server, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Nome per una credenziale nuova: il server, con un suffisso numerico se e' gia' preso.</summary>
    public static string SuggestId(string share, IEnumerable<string> existingIds)
    {
        var taken = new HashSet<string>(existingIds, StringComparer.OrdinalIgnoreCase);
        var basis = ServerOf(share);
        var id = basis;
        for (var n = 2; taken.Contains(id); n++) id = $"{basis}-{n}";
        return id;
    }
}
