using System.Text.Json;

namespace RoboKeep.Core.Services;

/// <summary>
/// Registro su file degli shadow ID creati da RoboKeep. Se un run VSS muore senza pulizia,
/// l'ID resta nel registro: il run VSS successivo lo passa al helper (elevato) che elimina
/// lo snapshot residuo. Stesso pattern della pulizia .inprogress del versioning.
/// </summary>
public sealed class VssLedger
{
    private readonly string _path;

    public VssLedger(string path) => _path = path;

    public IReadOnlyList<string> List()
    {
        if (!File.Exists(_path)) return Array.Empty<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_path)) ?? new();
        }
        catch { return Array.Empty<string>(); }
    }

    public void Add(string shadowId)
    {
        var ids = List().ToList();
        if (!ids.Contains(shadowId)) ids.Add(shadowId);
        Save(ids);
    }

    public void Remove(string shadowId)
    {
        var ids = List().Where(i => i != shadowId).ToList();
        Save(ids);
    }

    private void Save(List<string> ids)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_path, JsonSerializer.Serialize(ids));
    }
}
