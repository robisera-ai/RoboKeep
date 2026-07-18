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

    /// <summary>true se <paramref name="path"/> è una destinazione di rete: UNC (<c>\\server\…</c>)
    /// o unità mappata di rete. Serve a distinguere una share legittima da un disco locale
    /// staccato: <see cref="ForPath"/> restituisce null per entrambi, ma solo il secondo è un
    /// disco removibile da difendere. Best-effort come <see cref="ForPath"/>: mai un'eccezione.</summary>
    public static bool IsNetworkPath(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(root)) return false;
            if (root.StartsWith(@"\\", StringComparison.Ordinal)) return true;
            if (!root.EndsWith('\\')) root += '\\';
            // Un'unità mappata di rete resta di rete anche da scollegata; una lettera locale
            // inesistente dà NoRootDirectory, non Network: giustamente non è esente.
            return new DriveInfo(root).DriveType == DriveType.Network;
        }
        catch { return false; }
    }

    /// <summary>Esito della guardia per una destinazione, unico punto che unisce identificazione
    /// (impura, Windows) e decisione (<see cref="VolumeGuard"/>, pura). Le destinazioni di rete
    /// sono esentate: non hanno un volume removibile e la loro raggiungibilità la copre il
    /// pre-avvio, non la rotazione dei dischi. Averlo qui evita che <c>BackupRunner</c> e il
    /// calcolo dell'attesa nella lista replichino l'esenzione e la lascino divergere.</summary>
    public static VolumeCheck CheckDestination(string? destination, string? expectedId)
    {
        if (IsNetworkPath(destination)) return VolumeCheck.NoExpectation;
        return VolumeGuard.Check(expectedId, ForPath(destination));
    }
}
