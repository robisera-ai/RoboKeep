using System.Diagnostics;
using System.Text.Json;

namespace RoboKeep.Core.Services;

/// <summary>Voce del registro: shadow ID + PID del processo proprietario.</summary>
public sealed record VssLedgerEntry(string ShadowId, int OwnerPid);

/// <summary>
/// Registro su file degli shadow ID creati da RoboKeep, con il PID del processo che li ha
/// creati. Se un run VSS muore senza pulizia, l'ID resta nel registro: il run VSS successivo
/// elimina solo i residui il cui processo proprietario non esiste più, così due processi
/// RoboKeep concorrenti (GUI + attività pianificata) non si cancellano gli snapshot vivi
/// a vicenda. Stesso pattern della pulizia .inprogress del versioning.
/// </summary>
public sealed class VssLedger
{
    private readonly string _path;

    public VssLedger(string path) => _path = path;

    public IReadOnlyList<VssLedgerEntry> List()
    {
        if (!File.Exists(_path)) return Array.Empty<VssLedgerEntry>();
        try
        {
            return JsonSerializer.Deserialize<List<VssLedgerEntry>>(File.ReadAllText(_path)) ?? new();
        }
        catch { return Array.Empty<VssLedgerEntry>(); }
    }

    /// <summary>Shadow ID residui di processi non più vivi: sicuri da eliminare.</summary>
    public IReadOnlyList<string> ListStale()
        => List().Where(e => !IsProcessAlive(e.OwnerPid)).Select(e => e.ShadowId).ToList();

    public void Add(string shadowId)
    {
        var entries = List().ToList();
        if (!entries.Any(e => e.ShadowId == shadowId))
            entries.Add(new VssLedgerEntry(shadowId, Environment.ProcessId));
        Save(entries);
    }

    public void Remove(string shadowId)
    {
        Save(List().Where(e => e.ShadowId != shadowId).ToList());
    }

    private void Save(List<VssLedgerEntry> entries)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_path, JsonSerializer.Serialize(entries));
    }

    private static bool IsProcessAlive(int pid)
    {
        try { return !Process.GetProcessById(pid).HasExited; }
        catch { return false; }
    }
}
