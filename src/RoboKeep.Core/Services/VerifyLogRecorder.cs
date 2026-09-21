using System.Text;

namespace RoboKeep.Core.Services;

/// <summary>
/// Raccoglie le righe di una verifica integrità mentre le inoltra a video, e a fine lavoro le
/// salva come log a sé (<c>AAAAMMGG-HHMMSS-&lt;job&gt;-verifica.log[.zip]</c>) da collegare alla
/// voce "verifica" della cronologia. La verifica è un'operazione distinta dal backup: parte
/// quando il log del backup è già chiuso, compresso e magari spedito per email, e può anche
/// essere lanciata a mano senza alcun backup. Un log suo = ogni riga di cronologia ha il proprio.
/// Le righe di avanzamento ("verificati 50/16000...") restano solo a video: nel file sarebbero
/// centinaia di righe senza informazione.
/// </summary>
public sealed class VerifyLogRecorder : IProgress<string>
{
    private readonly IProgress<string>? _inner;
    private readonly StringBuilder _text = new();
    private readonly string _progressPrefix;

    public VerifyLogRecorder(IProgress<string>? inner)
    {
        _inner = inner;
        // Parte fissa di "[verifica] verificati {0}/{1}...", nella lingua corrente.
        var template = CoreLoc.S("Verify_Progress");
        var cut = template.IndexOf("{0}", StringComparison.Ordinal);
        _progressPrefix = cut > 0 ? template[..cut] : template;
    }

    public void Report(string value)
    {
        _inner?.Report(value);
        if (value.StartsWith(_progressPrefix, StringComparison.Ordinal)) return;
        lock (_text) _text.AppendLine(value);
    }

    /// <summary>Salva il log e ne restituisce il percorso (null se la scrittura fallisce: un log
    /// mancato non deve far fallire una verifica). Con <paramref name="result"/> valorizzato
    /// aggiunge l'elenco completo dei file differenti: a video se ne mostrano solo i primi.</summary>
    public string? Save(LogService log, string jobName, DateTime startedAt, VerifyResult? result)
    {
        try
        {
            var content = new StringBuilder();
            content.AppendLine($"===== {CoreLoc.S("Verify_LogTitle")} — {jobName} — {startedAt:g} =====");
            lock (_text) content.Append(_text);
            if (result is { MismatchedPaths.Count: > 0 })
            {
                content.AppendLine().AppendLine(CoreLoc.S("Verify_LogMismatchList"));
                foreach (var path in result.MismatchedPaths)
                    content.AppendLine("  " + path);
            }
            return log.WriteAndArchive($"{jobName}-{CoreLoc.S("Verify_LogSuffix")}", content.ToString(), startedAt);
        }
        catch { return null; }
    }
}
