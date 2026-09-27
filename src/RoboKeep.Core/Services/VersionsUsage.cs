using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace RoboKeep.Core.Services;

/// <summary>
/// Quanto occupano DAVVERO le versioni in una destinazione. Sommare le dimensioni delle cartelle
/// datate mentirebbe: con gli hard-link lo stesso file fisico compare in tutte le versioni in cui
/// non e' cambiato, e il totale risulterebbe molte volte lo spazio realmente usato (dividerlo per
/// il numero di cartelle sarebbe un'altra bugia). Qui ogni file fisico si conta UNA volta sola,
/// riconoscendolo dal suo identificatore NTFS (volume + file id, via GetFileInformationByHandle).
/// <para>Best-effort e a tempo: oltre il budget, o al primo imprevisto, restituisce null e chi
/// scrive il messaggio dice «n/d». Si chiama solo quando serve davvero (un run fallito per spazio):
/// costa un'enumerazione dell'intera destinazione.</para>
/// </summary>
public static class VersionsUsage
{
    /// <summary>Tetto di tempo: su una destinazione con centinaia di migliaia di file il conteggio
    /// esatto non vale un'attesa, e il messaggio all'utente non deve aspettarlo.</summary>
    public static readonly TimeSpan Budget = TimeSpan.FromSeconds(60);

    /// <summary>Byte occupati dalle VERSIONI in <paramref name="destination"/> (solo le cartelle
    /// datate valide: `RoboKeep-config`, le `.inprogress` e i residui `.deleting-…` non sono
    /// versioni e non vanno addebitati a chi decide quante tenerne), ogni file fisico contato una
    /// volta; null se non si e' potuto sapere (tempo scaduto, nessuna versione, errore).</summary>
    public static long? Measure(string? destination, TimeSpan? budget = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(destination) || !Directory.Exists(destination)) return null;
            var versions = SnapshotName.ListValid(destination);
            if (versions.Count == 0) return null;

            var cap = budget ?? Budget;
            var sw = Stopwatch.StartNew();
            // Un solo insieme per TUTTE le versioni: e' proprio tra una versione e l'altra che lo
            // stesso file fisico si ripete, ed e' quello che non va contato due volte.
            var seen = new HashSet<(uint Volume, ulong Index)>();
            long total = 0;
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint, // junction/symlink: non si seguono
                IgnoreInaccessible = true,
            };
            foreach (var version in versions)
                foreach (var file in Directory.EnumerateFiles(Path.Combine(destination, version), "*", options))
                {
                    if (sw.Elapsed > cap) return null;
                    if (Identify(file) is not { } info) continue; // file sparito o non apribile: si salta
                    // Un solo link = nessun altro nome punta a questo file: si conta senza passare
                    // dall'insieme. Non e' solo un risparmio: su exFAT/FAT il "file id" non e'
                    // affidabile, e due file distinti potrebbero sembrare lo stesso.
                    if (info.Links > 1 && !seen.Add((info.Volume, info.Index))) continue;
                    total += info.Size;
                }
            return total;
        }
        catch { return null; }
    }

    /// <summary>Dimensione leggibile da un umano ("1,4 GB"), oppure «n/d» quando non si sa.</summary>
    public static string Describe(long? bytes)
    {
        if (bytes is not { } b || b < 0) return CoreLoc.S("Space_Unknown");
        var units = new[] { "B", "KB", "MB", "GB", "TB", "PB" };
        double v = b;
        var i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        // I byte interi non hanno decimali da mostrare; da KB in su una cifra basta e avanza.
        return string.Format(CultureInfo.CurrentCulture, i == 0 ? "{0:0} {1}" : "{0:0.#} {1}", v, units[i]);
    }

    /// <summary>Identita' fisica, numero di hard-link e dimensione di un file: (volume, file id) sono
    /// la sua "targa" su NTFS, uguale per tutti i suoi hard-link. L'handle si apre con i soli
    /// attributi, cosi' un file aperto da qualcun altro non fa fallire il conteggio.</summary>
    private static (uint Volume, ulong Index, uint Links, long Size)? Identify(string path)
    {
        try
        {
            using var handle = CreateFileW(LongPath.Extended(path), FILE_READ_ATTRIBUTES,
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, IntPtr.Zero,
                OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, IntPtr.Zero);
            if (handle.IsInvalid) return null;
            if (!GetFileInformationByHandle(handle, out var info)) return null;
            var index = ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow;
            var size = ((long)info.FileSizeHigh << 32) | info.FileSizeLow;
            return (info.VolumeSerialNumber, index, info.NumberOfLinks, size);
        }
        catch { return null; }
    }

    private const uint FILE_READ_ATTRIBUTES = 0x0080;
    private const uint FILE_SHARE_READ = 0x1;
    private const uint FILE_SHARE_WRITE = 0x2;
    private const uint FILE_SHARE_DELETE = 0x4;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;

    // Pack = 4 obbligatorio: nativamente le tre date sono coppie di DWORD allineate a 4 byte, mentre
    // un long in C# si allineerebbe a 8 aggiungendo padding dopo FileAttributes e sfalsando TUTTI i
    // campi successivi (dimensioni e file id letti dal posto sbagliato).
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct BY_HANDLE_FILE_INFORMATION
    {
        public uint FileAttributes;
        public long CreationTime;
        public long LastAccessTime;
        public long LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
        uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);
}
