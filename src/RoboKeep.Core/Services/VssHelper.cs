using System.Diagnostics;
using System.Management;

namespace RoboKeep.Core.Services;

/// <summary>
/// Lato elevato della sessione VSS (gira in un processo con privilegi amministrativi,
/// lanciato da RoboKeep con --vss-helper). Crea lo snapshot del volume via WMI, espone
/// un symlink al device e resta vivo finché il client non segnala il rilascio, il padre
/// muore o scade il timeout di sicurezza. Pulisce sempre, anche in caso di errore.
/// </summary>
public static class VssHelper
{
    private static readonly TimeSpan SafetyTimeout = TimeSpan.FromHours(12);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    /// <summary>Punto di ingresso del processo elevato. Ritorna l'exit code.</summary>
    public static int Run(string sessionDir)
    {
        string? shadowId = null;
        string? linkDir = null;
        try
        {
            var request = VssSessionProtocol.ReadRequest(sessionDir);

            // Pulizia residui di run precedenti (best-effort: siamo già elevati, è il
            // momento giusto). Un ID inesistente non è un errore.
            foreach (var stale in request.StaleShadowIds)
                TryDeleteShadow(stale);

            shadowId = CreateShadow(request.Volume);
            var device = GetDeviceObject(shadowId);

            linkDir = VssSessionProtocol.LinkDir(sessionDir);
            // Il device path richiede il backslash finale per essere attraversabile.
            Directory.CreateSymbolicLink(linkDir, device + @"\");

            VssSessionProtocol.WriteReady(sessionDir, VssReady.Ok(shadowId, linkDir));

            WaitForRelease(sessionDir, request.ParentPid);
            return 0;
        }
        catch (Exception ex)
        {
            try { VssSessionProtocol.WriteReady(sessionDir, VssReady.Fail(ex.Message)); }
            catch { }
            return 1;
        }
        finally
        {
            if (linkDir is not null)
                try { Directory.Delete(linkDir); } catch { }
            if (shadowId is not null)
                TryDeleteShadow(shadowId);
        }
    }

    private static string CreateShadow(string volume)
    {
        using var shadowClass = new ManagementClass("Win32_ShadowCopy");
        using var inParams = shadowClass.GetMethodParameters("Create");
        inParams["Volume"] = volume;
        inParams["Context"] = "ClientAccessible";
        using var outParams = shadowClass.InvokeMethod("Create", inParams, null);

        var returnValue = Convert.ToInt32(outParams["ReturnValue"]);
        if (returnValue != 0)
            throw new VssUnavailableException($"Win32_ShadowCopy.Create fallita (codice {returnValue}).");
        return (string)outParams["ShadowID"];
    }

    private static string GetDeviceObject(string shadowId)
    {
        using var searcher = new ManagementObjectSearcher(
            $"SELECT DeviceObject FROM Win32_ShadowCopy WHERE ID = '{shadowId}'");
        foreach (ManagementObject shadow in searcher.Get())
            return (string)shadow["DeviceObject"];
        throw new VssUnavailableException($"Snapshot {shadowId} creato ma non trovato in WMI.");
    }

    private static void TryDeleteShadow(string shadowId)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT * FROM Win32_ShadowCopy WHERE ID = '{shadowId}'");
            foreach (ManagementObject shadow in searcher.Get())
                shadow.Delete();
        }
        catch { /* best-effort: l'ID resta nel ledger e verrà ritentato al prossimo run */ }
    }

    private static void WaitForRelease(string sessionDir, int parentPid)
    {
        var releaseFile = VssSessionProtocol.ReleaseFile(sessionDir);
        var deadline = DateTime.UtcNow + SafetyTimeout;
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(releaseFile)) return;
            if (!IsProcessAlive(parentPid)) return;
            Thread.Sleep(PollInterval);
        }
    }

    private static bool IsProcessAlive(int pid)
    {
        try { return !Process.GetProcessById(pid).HasExited; }
        catch { return false; }
    }
}
