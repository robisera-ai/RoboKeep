using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace RoboKeep.Core.Services;

/// <summary>
/// Cancellazione che tollera i file sola-lettura SENZA spegnerne l'attributo.
/// Le API .NET (File.Delete / Directory.Delete) lanciano UnauthorizedAccessException sui file
/// read-only; la soluzione ingenua (togliere il ReadOnly e poi cancellare) su un file hard-linkato
/// toccherebbe l'inode CONDIVISO, spegnendo il ReadOnly anche negli snapshot superstiti. Qui invece
/// si cancella il NOME col flag FILE_DISPOSITION_FLAG_IGNORE_READONLY_ATTRIBUTE: l'attributo
/// sull'inode resta intatto e gli altri hard-link conservano la loro protezione. Necessario perche'
/// robocopy (/COPY:DAT) copia l'attributo ReadOnly dei file dalla sorgente dentro ogni snapshot.
/// Richiede NTFS + Windows 10 1709 o successivo (sempre soddisfatto: il versioning richiede gia' NTFS).
/// </summary>
public static class FileSystemDelete
{
    // FILE_INFO_BY_HANDLE_CLASS.FileDispositionInfoEx
    private const int FileDispositionInfoEx = 21;
    private const uint DELETE = 0x00010000;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;   // necessario per aprire una directory
    private const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000; // non seguire junction/symlink: li cancella come foglia

    [Flags]
    private enum DispositionFlags : uint
    {
        Delete = 0x1,
        PosixSemantics = 0x2,
        IgnoreReadOnlyAttribute = 0x10,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileDispositionInfoExData
    {
        public DispositionFlags Flags;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
        uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(
        SafeFileHandle hFile, int fileInformationClass, ref FileDispositionInfoExData lpFileInformation, uint dwBufferSize);

    /// <summary>Cancella un file preservandone l'attributo ReadOnly sugli eventuali hard-link superstiti.</summary>
    public static void DeleteFile(string path)
    {
        if (!File.Exists(path)) return;
        DeleteLeaf(path, isDirectory: false);
    }

    /// <summary>Cancella ricorsivamente una directory (dal basso verso l'alto) preservando il ReadOnly
    /// dei file sugli hard-link superstiti. Le directory reparse point vengono rimosse come foglia.</summary>
    public static void DeleteDirectory(string path)
    {
        var di = new DirectoryInfo(path);
        if (!di.Exists) return;

        // Rinomina-prima-di-cancellare: se la cancellazione si interrompe a metà (file bloccato da
        // antivirus/indicizzazione, crash), il residuo ha un nome ".deleting-…" che NON è uno snapshot
        // valido (SnapshotName non lo riconosce), quindi non compare come versione ripristinabile.
        var target = di;
        var parent = di.Parent?.FullName;
        if (parent is not null)
        {
            try
            {
                var staged = Path.Combine(parent, di.Name + ".deleting-" + Guid.NewGuid().ToString("N")[..8]);
                Directory.Move(path, staged);
                target = new DirectoryInfo(staged);
            }
            catch { /* rename non riuscito (lock/permessi sulla cartella stessa): cancella sul posto */ }
        }

        DeleteDirectoryCore(target);
    }

    private static void DeleteDirectoryCore(DirectoryInfo dir)
    {
        if ((dir.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            DeleteLeaf(dir.FullName, isDirectory: true); // junction/symlink: cancella il link, non il contenuto puntato
            return;
        }

        foreach (var entry in dir.EnumerateFileSystemInfos())
        {
            if (entry is DirectoryInfo sub)
                DeleteDirectoryCore(sub);
            else
                DeleteLeaf(entry.FullName, isDirectory: false);
        }

        DeleteLeaf(dir.FullName, isDirectory: true);
    }

    private static void DeleteLeaf(string path, bool isDirectory)
    {
        if (TryPosixDelete(path, isDirectory)) return;

        // Fallback (filesystem senza FileDispositionInfoEx, es. FAT): ripristina il vecchio comportamento.
        // Tocca l'inode condiviso, ma su NTFS questo ramo non viene mai eseguito.
        var info = isDirectory ? (FileSystemInfo)new DirectoryInfo(path) : new FileInfo(path);
        if ((info.Attributes & FileAttributes.ReadOnly) != 0)
            info.Attributes &= ~FileAttributes.ReadOnly;
        if (isDirectory) Directory.Delete(path);
        else File.Delete(path);
    }

    private static bool TryPosixDelete(string path, bool isDirectory)
    {
        var flags = FILE_FLAG_OPEN_REPARSE_POINT;
        if (isDirectory) flags |= FILE_FLAG_BACKUP_SEMANTICS;

        const uint shareAll = 0x1 | 0x2 | 0x4; // FILE_SHARE_READ | WRITE | DELETE
        using var handle = CreateFileW(path, DELETE, shareAll, IntPtr.Zero, OPEN_EXISTING, flags, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            // Win32Exception come inner: traduce il codice nel messaggio giusto (IOException(string,int)
            // lo interpreterebbe invece come HRESULT, etichettando male l'errore).
            var openErr = Marshal.GetLastWin32Error();
            throw new IOException($"Impossibile aprire per la cancellazione: {path}", new Win32Exception(openErr));
        }

        var data = new FileDispositionInfoExData
        {
            Flags = DispositionFlags.Delete | DispositionFlags.PosixSemantics | DispositionFlags.IgnoreReadOnlyAttribute,
        };

        if (SetFileInformationByHandle(handle, FileDispositionInfoEx, ref data, (uint)Marshal.SizeOf<FileDispositionInfoExData>()))
            return true;

        // ERROR_INVALID_PARAMETER / NOT_SUPPORTED: il filesystem non conosce l'API -> lascia provare il fallback.
        var err = Marshal.GetLastWin32Error();
        const int ERROR_INVALID_PARAMETER = 87;
        const int ERROR_NOT_SUPPORTED = 50;
        if (err is ERROR_INVALID_PARAMETER or ERROR_NOT_SUPPORTED)
            return false;

        throw new IOException($"Cancellazione fallita: {path}", new Win32Exception(err));
    }
}
