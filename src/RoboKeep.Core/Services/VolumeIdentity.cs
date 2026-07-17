using System.Runtime.InteropServices;
using System.Text;

namespace RoboKeep.Core.Services;

/// <summary>Identità di un volume: identificativo univoco (immutabile) ed etichetta leggibile.</summary>
public sealed record VolumeInfo(string VolumeId, string Label);

/// <summary>
/// Legge l'identità del volume che ospita un percorso. Serve alla rotazione dei dischi: due
/// dischi esterni alternati prendono spesso la stessa lettera (E:), ma hanno identificativi
/// di volume diversi — assegnati alla formattazione e indipendenti dalla lettera.
/// Best-effort: qualunque problema restituisce null (nessun controllo possibile), mai
/// un'eccezione: una diagnostica non deve poter impedire un backup legittimo.
/// </summary>
public static class VolumeIdentity
{
    // Restituisce il nome del volume nella forma \\?\Volume{GUID}\ per un mount point (es. "E:\").
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeNameForVolumeMountPointW(
        string lpszVolumeMountPoint, StringBuilder lpszVolumeName, int cchBufferLength);

    /// <summary>Identità del volume che ospita <paramref name="path"/>, o null se non
    /// determinabile: percorso vuoto, UNC (nessun volume locale), disco non collegato.</summary>
    public static VolumeInfo? ForPath(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(root)) return null;
            // Share di rete: nessun volume locale da identificare.
            if (root.StartsWith(@"\\", StringComparison.Ordinal)) return null;
            // L'API richiede il backslash finale ("E:\", non "E:").
            if (!root.EndsWith('\\')) root += '\\';

            var buffer = new StringBuilder(64);
            if (!GetVolumeNameForVolumeMountPointW(root, buffer, buffer.Capacity)) return null;

            var id = buffer.ToString();
            if (string.IsNullOrEmpty(id)) return null;

            // L'etichetta è solo informativa (modificabile e duplicabile): mai un criterio
            // di confronto. Un volume senza etichetta è legittimo.
            var label = "";
            try { label = new DriveInfo(root).VolumeLabel ?? ""; } catch { }

            return new VolumeInfo(id, label);
        }
        catch { return null; }
    }
}
