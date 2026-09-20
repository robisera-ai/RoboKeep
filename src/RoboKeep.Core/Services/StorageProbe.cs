using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace RoboKeep.Core.Services;

/// <summary>Tipo di supporto fisico che ospita un percorso, per quel che serve a dosare il carico.</summary>
public enum DiskMedia
{
    /// <summary>Non determinabile (rete, volume composito, errore): nessuna limitazione.</summary>
    Unknown,
    /// <summary>Disco meccanico, o disco USB che non si lascia identificare (trattato da meccanico).</summary>
    Hdd,
    /// <summary>Stato solido: nessuna penalità di posizionamento.</summary>
    Ssd,
}

/// <summary>
/// Interroga Windows sul supporto fisico dietro un percorso (IOCTL_STORAGE_QUERY_PROPERTY: non
/// richiede privilegi di amministratore). Serve a non lanciare 8-16 thread di copia su un disco
/// meccanico, dove il parallelismo si traduce solo in salti continui della testina.
/// Best-effort come <see cref="VolumeIdentity"/>: qualunque problema restituisce
/// <see cref="DiskMedia.Unknown"/>, mai un'eccezione.
/// </summary>
public static class StorageProbe
{
    /// <summary>Thread massimi quando è coinvolto un disco meccanico (stesso valore del wizard).</summary>
    public const int HddMaxThreads = 2;

    /// <summary>Decisione pura a partire da ciò che il dispositivo dichiara.
    /// <paramref name="seekPenalty"/>: true = meccanico, false = stato solido, null = il
    /// dispositivo non risponde (tipico dei bridge USB). In quel caso, su USB, fa fede il TRIM:
    /// chi lo supporta è un SSD; chi non lo supporta viene trattato da disco meccanico — la
    /// scelta prudente: limitare i thread a un SSD costa qualche minuto, non limitarli a un
    /// HDD lo maltratta.</summary>
    public static DiskMedia Classify(bool? seekPenalty, bool isUsb, bool? trimEnabled)
    {
        if (seekPenalty == true) return DiskMedia.Hdd;
        if (seekPenalty == false) return DiskMedia.Ssd;
        if (!isUsb) return DiskMedia.Unknown;
        return trimEnabled == true ? DiskMedia.Ssd : DiskMedia.Hdd;
    }

    /// <summary>Tetto ai thread di copia dato il supporto di sorgente e destinazione:
    /// <see cref="HddMaxThreads"/> se almeno uno dei due è meccanico, altrimenti null (nessun tetto).</summary>
    public static int? ThreadCap(DiskMedia source, DiskMedia destination) =>
        source == DiskMedia.Hdd || destination == DiskMedia.Hdd ? HddMaxThreads : null;

    /// <summary>Tipo di supporto che ospita <paramref name="path"/>.</summary>
    public static DiskMedia Detect(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return DiskMedia.Unknown;
            if (VolumeIdentity.IsNetworkPath(path)) return DiskMedia.Unknown;

            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(root) || root.Length < 2 || root[1] != ':') return DiskMedia.Unknown;

            // "\\.\E:" = il volume; accesso 0 = sola interrogazione, non serve l'elevazione.
            using var handle = CreateFileW($@"\\.\{root[0]}:", 0, FileShareReadWrite, IntPtr.Zero,
                OpenExisting, 0, IntPtr.Zero);
            if (handle.IsInvalid) return DiskMedia.Unknown;

            var seek = QueryFlag(handle, StorageDeviceSeekPenaltyProperty);
            // BusType e TRIM servono solo se il dispositivo tace sulla penalità di posizionamento.
            var isUsb = seek is null && QueryBusType(handle) == BusTypeUsb;
            var trim = isUsb ? QueryFlag(handle, StorageDeviceTrimProperty) : null;
            return Classify(seek, isUsb, trim);
        }
        catch { return DiskMedia.Unknown; }
    }

    private const uint FileShareReadWrite = 0x1 | 0x2;
    private const uint OpenExisting = 3;
    private const uint IoctlStorageQueryProperty = 0x002D1400;
    private const int StorageDeviceProperty = 0;
    private const int StorageDeviceSeekPenaltyProperty = 7;
    private const int StorageDeviceTrimProperty = 8;
    private const int BusTypeUsb = 7;

    // STORAGE_PROPERTY_QUERY: PropertyId, QueryType (0 = PropertyStandardQuery), AdditionalParameters[1].
    [StructLayout(LayoutKind.Sequential)]
    private struct StoragePropertyQuery
    {
        public int PropertyId;
        public int QueryType;
        public byte AdditionalParameters;
    }

    // DEVICE_SEEK_PENALTY_DESCRIPTOR e DEVICE_TRIM_DESCRIPTOR hanno la stessa forma:
    // Version, Size, un BOOLEAN.
    [StructLayout(LayoutKind.Sequential)]
    private struct FlagDescriptor
    {
        public uint Version;
        public uint Size;
        [MarshalAs(UnmanagedType.U1)] public bool Flag;
    }

    private static bool? QueryFlag(SafeFileHandle handle, int propertyId)
    {
        var query = new StoragePropertyQuery { PropertyId = propertyId };
        if (!DeviceIoControl(handle, IoctlStorageQueryProperty, ref query, (uint)Marshal.SizeOf<StoragePropertyQuery>(),
                out FlagDescriptor result, (uint)Marshal.SizeOf<FlagDescriptor>(), out _, IntPtr.Zero))
            return null;
        return result.Flag;
    }

    private static int? QueryBusType(SafeFileHandle handle)
    {
        var query = new StoragePropertyQuery { PropertyId = StorageDeviceProperty };
        var buffer = new byte[1024];
        if (!DeviceIoControl(handle, IoctlStorageQueryProperty, ref query, (uint)Marshal.SizeOf<StoragePropertyQuery>(),
                buffer, (uint)buffer.Length, out var returned, IntPtr.Zero))
            return null;
        // STORAGE_DEVICE_DESCRIPTOR.BusType sta all'offset 28.
        return returned >= 32 ? BitConverter.ToInt32(buffer, 28) : null;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
        uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice, uint dwIoControlCode, ref StoragePropertyQuery lpInBuffer, uint nInBufferSize,
        out FlagDescriptor lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice, uint dwIoControlCode, ref StoragePropertyQuery lpInBuffer, uint nInBufferSize,
        byte[] lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);
}
