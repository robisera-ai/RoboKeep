using System.Text.Json;

namespace RoboKeep.Core.Services;

/// <summary>Richiesta del client al helper elevato: volume da fotografare, PID del padre da
/// sorvegliare e snapshot residui di run precedenti da eliminare.</summary>
public sealed record VssRequest(string Volume, int ParentPid, List<string> StaleShadowIds);

/// <summary>Esito della creazione snapshot scritto dal helper.</summary>
public sealed record VssReady(bool Success, string? ShadowId, string? LinkPath, string? Error)
{
    public static VssReady Ok(string shadowId, string linkPath) => new(true, shadowId, linkPath, null);
    public static VssReady Fail(string error) => new(false, null, null, error);
}

/// <summary>
/// Protocollo file della sessione VSS tra RoboKeep (utente) e il helper elevato:
/// request.json (client→helper), ready.json (helper→client), release.flag (client→helper),
/// e la cartella del symlink verso il device dello snapshot.
/// </summary>
public static class VssSessionProtocol
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string RequestFile(string sessionDir) => Path.Combine(sessionDir, "request.json");
    public static string ReadyFile(string sessionDir) => Path.Combine(sessionDir, "ready.json");
    public static string ReleaseFile(string sessionDir) => Path.Combine(sessionDir, "release.flag");
    public static string LinkDir(string sessionDir) => Path.Combine(sessionDir, "source");

    public static void WriteRequest(string sessionDir, VssRequest request) =>
        File.WriteAllText(RequestFile(sessionDir), JsonSerializer.Serialize(request, Options));

    public static VssRequest ReadRequest(string sessionDir) =>
        JsonSerializer.Deserialize<VssRequest>(File.ReadAllText(RequestFile(sessionDir)), Options)
            ?? throw new InvalidDataException("request.json vuoto o non valido.");

    public static void WriteReady(string sessionDir, VssReady ready) =>
        File.WriteAllText(ReadyFile(sessionDir), JsonSerializer.Serialize(ready, Options));

    /// <summary>null se il file non esiste ancora (helper non pronto).</summary>
    public static VssReady? ReadReady(string sessionDir)
    {
        var path = ReadyFile(sessionDir);
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<VssReady>(File.ReadAllText(path), Options);
    }

    public static void SignalRelease(string sessionDir) =>
        File.WriteAllText(ReleaseFile(sessionDir), "");
}
