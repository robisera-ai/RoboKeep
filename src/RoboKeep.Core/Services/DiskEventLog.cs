using System.Diagnostics.Eventing.Reader;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace RoboKeep.Core.Services;

/// <summary>Errori disco che Windows ha registrato di recente per un'unità.</summary>
/// <param name="BadBlocks">Blocchi danneggiati / previsione di guasto (disk 7, 52).</param>
/// <param name="IoErrors">Errori di I/O, del controller, operazioni ritentate (disk 11, 51, 153):
/// tipici anche di un disco che "sparisce" per cavo, box o alimentazione.</param>
/// <param name="FileSystemErrors">Scritture perse e corruzione NTFS (Ntfs 50, 55, 137, 140).</param>
/// <param name="Latest">Momento dell'errore più recente.</param>
public sealed record DiskEventSummary(int BadBlocks, int IoErrors, int FileSystemErrors, DateTime? Latest)
{
    public static readonly DiskEventSummary None = new(0, 0, 0, null);
    public int Total => BadBlocks + IoErrors + FileSystemErrors;

    /// <summary>Riga leggibile e localizzata con i conteggi, per avvisi e log.</summary>
    public string Describe() => string.Format(CoreLoc.S("Health_Detail"),
        BadBlocks, IoErrors, FileSystemErrors, Latest?.ToString("g") ?? "-");
}

/// <summary>
/// "Salute del disco" letta dal registro eventi di Windows. Lo SMART vero di un disco USB richiede
/// privilegi di amministratore (e Windows, senza, dichiara "Healthy" anche un disco con settori
/// pendenti): il registro Sistema invece si legge da utente normale e conserva per settimane
/// blocchi danneggiati, errori di I/O e scritture perse — i segnali che precedono un disastro.
/// Limite dichiarato: gli eventi nominano il disco per numero (\Device\HarddiskN) o per lettera,
/// non per identità fisica; due dischi alternati sulla stessa lettera si confondono. Per questo
/// la finestra è corta e si scartano gli eventi precedenti alla formattazione del volume.
/// Best-effort: qualunque problema restituisce "nessun evento", mai un'eccezione.
/// </summary>
public static class DiskEventLog
{
    /// <summary>Finestra di osservazione predefinita.</summary>
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromDays(14);

    private enum Kind { None, BadBlock, Io, FileSystem }

    /// <summary>Classifica un evento del registro Sistema (funzione pura).</summary>
    private static Kind Classify(string provider, int id) => provider switch
    {
        "disk" => id switch { 7 or 52 => Kind.BadBlock, 11 or 51 or 153 => Kind.Io, _ => Kind.None },
        "Ntfs" or "Microsoft-Windows-Ntfs" => id is 50 or 55 or 137 or 140 ? Kind.FileSystem : Kind.None,
        _ => Kind.None,
    };

    /// <summary>true se il messaggio dell'evento riguarda il disco numero <paramref name="diskNumber"/>
    /// o l'unità <paramref name="driveLetter"/> (funzione pura). Gli eventi "disk" citano
    /// <c>\Device\HarddiskN\DRx</c>; quelli NTFS la lettera (<c>E:</c>, <c>E:\$Mft</c>).</summary>
    public static bool Concerns(string provider, string message, int? diskNumber, char? driveLetter)
    {
        if (string.IsNullOrEmpty(message)) return false;
        if (provider == "disk")
            return diskNumber is int n
                && Regex.IsMatch(message, $@"\\Harddisk{n}\\", RegexOptions.IgnoreCase);
        return driveLetter is char c
            && Regex.IsMatch(message, $@"(?<![A-Za-z]){char.ToUpperInvariant(c)}:", RegexOptions.IgnoreCase);
    }

    /// <summary>Somma una sequenza di eventi già filtrati (funzione pura, usata anche dai test).</summary>
    public static DiskEventSummary Summarize(IEnumerable<(string Provider, int Id, DateTime When)> events)
    {
        int bad = 0, io = 0, fs = 0;
        DateTime? latest = null;
        foreach (var e in events)
        {
            switch (Classify(e.Provider, e.Id))
            {
                case Kind.BadBlock: bad++; break;
                case Kind.Io: io++; break;
                case Kind.FileSystem: fs++; break;
                default: continue;
            }
            if (latest is null || e.When > latest) latest = e.When;
        }
        return new DiskEventSummary(bad, io, fs, latest);
    }

    /// <summary>Validità della cache per lettera: la lettura del registro costa secondi, e lo
    /// stesso disco viene interrogato dal pre-avvio e poi dal runner, per ogni job in coda.</summary>
    public static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

    private static readonly Dictionary<(char, TimeSpan), (DateTime At, DiskEventSummary Summary)> _cache = new();

    /// <summary>Svuota la cache (test, o dopo una riattivazione manuale dei dischi).</summary>
    public static void ClearCache()
    {
        lock (_cache) _cache.Clear();
    }

    /// <summary>Errori recenti registrati per il disco locale che ospita <paramref name="path"/>.</summary>
    public static DiskEventSummary Collect(string? path, TimeSpan? window = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || VolumeIdentity.IsNetworkPath(path)) return DiskEventSummary.None;
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(root) || root.Length < 2 || root[1] != ':') return DiskEventSummary.None;
            if (!Directory.Exists(root)) return DiskEventSummary.None;

            var key = (char.ToUpperInvariant(root[0]), window ?? DefaultWindow);
            lock (_cache)
            {
                if (_cache.TryGetValue(key, out var hit) && DateTime.Now - hit.At < CacheTtl)
                    return hit.Summary;
            }
            var summary = Read(root[0], window);
            lock (_cache) _cache[key] = (DateTime.Now, summary);
            return summary;
        }
        catch { return DiskEventSummary.None; }
    }

    private static DiskEventSummary Read(char letter, TimeSpan? window)
    {
        try
        {
            var root = $"{letter}:\\";
            var diskNumber = GetDiskNumber(letter);
            var since = DateTime.Now - (window ?? DefaultWindow);
            // La radice di un volume nasce con la formattazione: gli eventi precedenti riguardano
            // per forza un altro volume che usava la stessa lettera (o questo disco prima di
            // essere rifatto da zero).
            try
            {
                var formatted = Directory.GetCreationTime(root);
                if (formatted > since && formatted <= DateTime.Now) since = formatted;
            }
            catch { /* senza data di formattazione resta la finestra */ }

            var ms = (long)Math.Ceiling((DateTime.Now - since).TotalMilliseconds);
            var query = new EventLogQuery("System", PathType.LogName,
                "*[System[(Level=1 or Level=2 or Level=3) and " +
                "Provider[@Name='disk' or @Name='Ntfs' or @Name='Microsoft-Windows-Ntfs'] and " +
                $"TimeCreated[timediff(@SystemTime) <= {ms}]]]");

            var matched = new List<(string, int, DateTime)>();
            using var reader = new EventLogReader(query);
            for (var ev = reader.ReadEvent(); ev is not null; ev = reader.ReadEvent())
            {
                using (ev)
                {
                    var provider = ev.ProviderName ?? "";
                    if (Classify(provider, ev.Id) == Kind.None) continue;
                    string message;
                    try { message = ev.FormatDescription() ?? ""; } catch { continue; }
                    if (Concerns(provider, message, diskNumber, letter))
                        matched.Add((provider, ev.Id, ev.TimeCreated ?? DateTime.Now));
                }
            }
            return Summarize(matched);
        }
        catch { return DiskEventSummary.None; }
    }

    // Numero del disco fisico dietro una lettera (IOCTL_STORAGE_GET_DEVICE_NUMBER, senza elevazione).
    private static int? GetDiskNumber(char letter)
    {
        using var handle = CreateFileW($@"\\.\{letter}:", 0, 0x1 | 0x2, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (handle.IsInvalid) return null;
        if (!DeviceIoControl(handle, 0x002D1080, IntPtr.Zero, 0, out StorageDeviceNumber n,
                (uint)Marshal.SizeOf<StorageDeviceNumber>(), out _, IntPtr.Zero))
            return null;
        return (int)n.DeviceNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StorageDeviceNumber
    {
        public uint DeviceType;
        public uint DeviceNumber;
        public uint PartitionNumber;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
        uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize,
        out StorageDeviceNumber lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);
}
