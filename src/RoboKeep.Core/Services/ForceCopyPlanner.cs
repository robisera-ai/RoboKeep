using System.Security.Cryptography;
using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>
/// Piano della passata "Forza copia": <paramref name="Filters"/> sono i filtri robocopy
/// (pattern in modalità semplice, nomi dei file cambiati in smart); <paramref name="NewHashes"/>
/// sono gli hash correnti di tutti i file abbinati (solo smart, da salvare a copia riuscita).
/// </summary>
public sealed record ForceCopyPlan(
    IReadOnlyList<string> Filters,
    IReadOnlyDictionary<string, string> NewHashes,
    bool Smart);

/// <summary>
/// Decide quali file forzare in copia. In modalità semplice restituisce i pattern così come sono.
/// In modalità smart enumera i file abbinati, ne calcola l'hash SHA256 e restituisce solo quelli
/// il cui hash è cambiato rispetto all'ultimo backup (via <see cref="ForceCopyHashStore"/>).
/// </summary>
public sealed class ForceCopyPlanner
{
    private readonly ForceCopyHashStore _store;

    public ForceCopyPlanner(ForceCopyHashStore store) => _store = store;

    public async Task<ForceCopyPlan> PlanAsync(
        BackupJob job, IProgress<string>? progress, CancellationToken ct)
    {
        var patterns = (job.ForceCopyFiles ?? new())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .ToList();

        var empty = new Dictionary<string, string>();

        if (patterns.Count == 0)
            return new ForceCopyPlan(Array.Empty<string>(), empty, job.ForceCopySmart);

        if (!job.ForceCopySmart)
            return new ForceCopyPlan(patterns, empty, false);

        var source = (job.Source ?? "").Trim();
        if (!Directory.Exists(source))
            return new ForceCopyPlan(Array.Empty<string>(), empty, true);

        var stored = _store.Load(job.Name);
        var newHashes = new Dictionary<string, string>();
        var changed = new List<string>();

        foreach (var file in EnumerateMatches(source, patterns))
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"[hash] {Path.GetFileName(file)}");
            var hash = await ComputeHashAsync(file, ct).ConfigureAwait(false);
            newHashes[file] = hash;

            if (!stored.TryGetValue(file, out var old) ||
                !string.Equals(old, hash, StringComparison.OrdinalIgnoreCase))
            {
                changed.Add(Path.GetFileName(file));
            }
        }

        return new ForceCopyPlan(changed.Distinct(StringComparer.OrdinalIgnoreCase).ToList(), newHashes, true);
    }

    /// <summary>Salva gli hash dopo una copia riuscita.</summary>
    public void Commit(string jobName, IReadOnlyDictionary<string, string> newHashes) =>
        _store.Save(jobName, newHashes);

    private static IEnumerable<string> EnumerateMatches(string source, IEnumerable<string> patterns)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pattern in patterns)
        {
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(source, pattern, SearchOption.AllDirectories); }
            catch { continue; }
            foreach (var f in files)
                set.Add(f);
        }
        return set;
    }

    private static async Task<string> ComputeHashAsync(string path, CancellationToken ct)
    {
        await using var fs = File.OpenRead(path);
        var bytes = await SHA256.HashDataAsync(fs, ct).ConfigureAwait(false);
        return Convert.ToHexString(bytes);
    }
}
