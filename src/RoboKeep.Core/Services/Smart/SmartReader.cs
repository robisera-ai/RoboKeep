using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace RoboKeep.Core.Services.Smart;

/// <summary>
/// Accesso ai dischi fisici. <see cref="ListPhysicalDisks"/> e <see cref="ReadNvme"/> non
/// richiedono privilegi; <see cref="ReadAta"/> (ATA PASS-THROUGH via SCSI pass-through, l'unica
/// via che attraversa i box USB) apre il disco in lettura+scrittura e richiede l'amministratore:
/// lo chiama solo il helper elevato. Tutto best-effort: null su errore, mai eccezioni.
/// </summary>
public static class SmartReader
{
    private const uint GenericRead = 0x80000000, GenericWrite = 0x40000000;
    private const uint ShareRW = 0x1 | 0x2, OpenExisting = 3;
    private const uint IoctlStorageQueryProperty = 0x002D1400;
    private const uint IoctlScsiPassThroughDirect = 0x0004D014;
    private const uint IoctlStorageGetDeviceNumber = 0x002D1080;
    public const int MaxDrives = 16;

    public static string DevicePath(int drive) => $@"\\.\PhysicalDrive{drive}";

    /// <summary>Dischi fisici presenti (0..MaxDrives-1 che si aprono), con modello, seriale, bus e
    /// lettere delle unita' che ospitano.</summary>
    public static List<DiskReport> ListPhysicalDisks()
    {
        var letters = LettersByDisk();
        var list = new List<DiskReport>();
        for (var n = 0; n < MaxDrives; n++)
        {
            using var h = CreateFileW(DevicePath(n), 0, ShareRW, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (h.IsInvalid) continue;
            var (model, serial, bus) = QueryDevice(h);
            list.Add(new DiskReport(n, model, serial, bus, letters.TryGetValue(n, out var l) ? l.ToArray() : Array.Empty<string>(), null, null, null));
        }
        return list;
    }

    /// <summary>Blocco SMART ATA (512 byte) via SCSI pass-through, o null. Richiede admin.</summary>
    public static byte[]? ReadAta(int drive)
    {
        try
        {
            using var h = CreateFileW(DevicePath(drive), GenericRead | GenericWrite, ShareRW, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (h.IsInvalid) return null;
            var data = Marshal.AllocHGlobal(512);
            try
            {
                // Buffer azzerato: su un disco che non risponde (NVMe) resterebbe spazzatura.
                for (var i = 0; i < 512; i += 8) Marshal.WriteInt64(data, i, 0);
                // SCSI_PASS_THROUGH_DIRECT (x64, 56 byte) + 32 byte di sense.
                var buf = new byte[56 + 32];
                BitConverter.GetBytes((ushort)56).CopyTo(buf, 0);
                buf[6] = 16;  // CdbLength
                buf[7] = 32;  // SenseInfoLength
                buf[8] = 1;   // SCSI_IOCTL_DATA_IN
                BitConverter.GetBytes((uint)512).CopyTo(buf, 12);  // DataTransferLength
                BitConverter.GetBytes((uint)10).CopyTo(buf, 16);   // TimeOutValue (s)
                BitConverter.GetBytes((long)data).CopyTo(buf, 24); // DataBuffer
                BitConverter.GetBytes((uint)56).CopyTo(buf, 32);   // SenseInfoOffset
                // ATA PASS-THROUGH(16): SMART READ DATA (B0h, features D0h, LBA 4Fh/C2h), PIO data-in, 1 blocco.
                new byte[] { 0x85, 0x08, 0x0E, 0, 0xD0, 0, 1, 0, 0, 0, 0x4F, 0, 0xC2, 0xA0, 0xB0, 0 }.CopyTo(buf, 36);
                if (!DeviceIoControl(h, IoctlScsiPassThroughDirect, buf, (uint)buf.Length, buf, (uint)buf.Length, out _, IntPtr.Zero)) return null;
                if (buf[2] != 0) return null; // ScsiStatus != GOOD: il dispositivo non ha risposto al comando ATA
                var block = new byte[512];
                Marshal.Copy(data, block, 0, 512);
                return SmartAttributes.ParseAta(block) is null ? null : block;
            }
            finally { Marshal.FreeHGlobal(data); }
        }
        catch { return null; }
    }

    /// <summary>Log SMART/Health NVMe (512 byte), o null. Non richiede admin.</summary>
    public static byte[]? ReadNvme(int drive)
    {
        try
        {
            using var h = CreateFileW(DevicePath(drive), 0, ShareRW, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (h.IsInvalid) return null;
            const int p = 8;                             // STORAGE_PROTOCOL_SPECIFIC_DATA
            const int header = p + 40;                   // query + descrittore, prima del log
            var buf = new byte[header + 512];
            BitConverter.GetBytes(50).CopyTo(buf, 0);   // StorageDeviceProtocolSpecificProperty
            BitConverter.GetBytes(0).CopyTo(buf, 4);    // PropertyStandardQuery
            BitConverter.GetBytes(3).CopyTo(buf, p);     // ProtocolTypeNvme
            BitConverter.GetBytes(2).CopyTo(buf, p + 4); // NVMeDataTypeLogPage
            BitConverter.GetBytes(2).CopyTo(buf, p + 8); // log page 2 = SMART/Health
            BitConverter.GetBytes(0).CopyTo(buf, p + 12);
            BitConverter.GetBytes(40).CopyTo(buf, p + 16);  // ProtocolDataOffset (dall'inizio del descrittore)
            BitConverter.GetBytes(512).CopyTo(buf, p + 20); // ProtocolDataLength
            if (!DeviceIoControl(h, IoctlStorageQueryProperty, buf, (uint)buf.Length, buf, (uint)buf.Length, out var ret, IntPtr.Zero)) return null;
            if (ret < header + 512) return null;
            // Il driver riscrive offset e lunghezza nel descrittore restituito: si legge da li', non
            // dai valori chiesti, perche' non tutti i driver mettono il log dove lo si e' chiesto.
            var offset = BitConverter.ToInt32(buf, p + 16);
            var length = BitConverter.ToInt32(buf, p + 20);
            if (length < 512 || offset < 0 || p + offset + 512 > buf.Length) return null;
            var log = new byte[512];
            Array.Copy(buf, p + offset, log, 0, 512);
            // Log tutto a zero = il driver ha risposto senza riempirlo (temperatura 0 K, riserva 0 %):
            // sarebbe un referto falso, peggio di nessun referto.
            return Array.TrueForAll(log, b => b == 0) ? null : log;
        }
        catch { return null; }
    }

    // STORAGE_DEVICE_DESCRIPTOR via StorageDeviceProperty (0). Offset dei campi calcolati dalla
    // dichiarazione (winioctl.h): Version 0, Size 4, DeviceType 8, DeviceTypeModifier 9,
    // RemovableMedia 10, CommandQueueing 11 (i quattro impacchettati a byte), poi i DWORD
    // VendorIdOffset 12, ProductIdOffset 16, ProductRevisionOffset 20, SerialNumberOffset 24,
    // BusType 28, RawPropertiesLength 32, RawDeviceProperties 36.
    private static (string Model, string Serial, DiskBus Bus) QueryDevice(SafeFileHandle h)
    {
        try
        {
            var query = new byte[12]; // PropertyId 0, QueryType 0, AdditionalParameters
            var buf = new byte[1024];
            if (!DeviceIoControl(h, IoctlStorageQueryProperty, query, 12, buf, (uint)buf.Length, out var ret, IntPtr.Zero) || ret < 36)
                return ("?", "", DiskBus.Unknown);
            string At(int offsetField)
            {
                var off = BitConverter.ToInt32(buf, offsetField);
                if (off <= 0 || off >= ret) return "";
                var end = Array.IndexOf(buf, (byte)0, off);
                return System.Text.Encoding.ASCII.GetString(buf, off, (end < 0 ? (int)ret : end) - off).Trim();
            }
            var bus = BitConverter.ToInt32(buf, 28) switch { 17 => DiskBus.Nvme, 11 => DiskBus.Sata, 7 => DiskBus.Usb, 3 => DiskBus.Sata /*ATA*/, _ => DiskBus.Other };
            var vendor = At(12);
            var product = At(16);
            var model = string.IsNullOrEmpty(vendor) ? product : $"{vendor} {product}".Trim();
            return (string.IsNullOrEmpty(model) ? "?" : model, At(24), bus);
        }
        catch { return ("?", "", DiskBus.Unknown); }
    }

    // Lettera -> numero disco, via IOCTL_STORAGE_GET_DEVICE_NUMBER sul volume.
    private static Dictionary<int, List<string>> LettersByDisk()
    {
        var map = new Dictionary<int, List<string>>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (d.DriveType != DriveType.Fixed && d.DriveType != DriveType.Removable) continue;
                using var h = CreateFileW($@"\\.\{d.Name[0]}:", 0, ShareRW, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
                if (h.IsInvalid) continue;
                var outBuf = new byte[12];
                if (!DeviceIoControl(h, IoctlStorageGetDeviceNumber, null, 0, outBuf, 12, out _, IntPtr.Zero)) continue;
                var n = BitConverter.ToInt32(outBuf, 4);
                if (!map.TryGetValue(n, out var list)) map[n] = list = new List<string>();
                list.Add(d.Name.TrimEnd('\\'));
            }
            catch { /* unita' non pronta: si salta */ }
        }
        return map;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, byte[]? lpInBuffer, uint nInBufferSize,
        byte[] lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);
}
