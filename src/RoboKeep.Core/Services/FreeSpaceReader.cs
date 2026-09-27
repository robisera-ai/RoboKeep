using System.Runtime.InteropServices;

namespace RoboKeep.Core.Services;

/// <summary>
/// Spazio libero del volume che ospita un percorso, via <c>GetDiskFreeSpaceEx</c>: vale anche per
/// le share UNC, dove <c>DriveInfo</c> non arriva, e restituisce la quota disponibile A CHI CHIAMA
/// (le quote NTFS possono renderla minore dello spazio libero del volume).
/// <para>null = non determinabile. Chi legge NON deve dedurne "disco pieno": deve rinunciare al
/// controllo. E' un punto di innesto iniettabile, cosi' i test possono decidere lo spazio.</para>
/// </summary>
public static class FreeSpaceReader
{
    /// <summary>Byte liberi sul volume di <paramref name="path"/>, o null se non si sa.</summary>
    public static long? Read(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            // Si interroga PRIMA il percorso stesso: GetDiskFreeSpaceEx accetta una cartella
            // qualsiasi, e su una share con punti di mount o reindirizzamenti la cartella del backup
            // puo' stare su un volume diverso dalla sua radice. Se la cartella non esiste ancora
            // (destinazione nuova) la chiamata fallisce, e si ripiega sulla radice.
            if (Query(path) is { } atPath) return atPath;
            var root = Path.GetPathRoot(path);
            return string.IsNullOrEmpty(root) || string.Equals(root, path, StringComparison.OrdinalIgnoreCase)
                ? null
                : Query(root);
        }
        catch { return null; }
    }

    // La barra finale non e' cosmetica: GetDiskFreeSpaceEx vuole un nome di CARTELLA, e su un
    // percorso come "E:" (senza barra) risponderebbe per la directory corrente di quel drive.
    private static long? Query(string path)
    {
        var dir = path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
        return GetDiskFreeSpaceEx(dir, out var freeForCaller, out _, out _) ? (long)freeForCaller : null;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceEx(
        string lpDirectoryName,
        out ulong lpFreeBytesAvailableToCaller,
        out ulong lpTotalNumberOfBytes,
        out ulong lpTotalNumberOfFreeBytes);
}
