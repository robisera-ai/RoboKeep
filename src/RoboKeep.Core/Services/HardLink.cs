using System.Runtime.InteropServices;

namespace RoboKeep.Core.Services;

/// <summary>Primitiva per creare hard-link NTFS (P/Invoke CreateHardLinkW).</summary>
public static class HardLink
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    /// <summary>Crea un hard-link <paramref name="linkPath"/> che punta a <paramref name="targetPath"/>. Lancia in caso di errore.</summary>
    public static void Create(string linkPath, string targetPath)
    {
        // Prefisso \\?\ su entrambi i percorsi: senza, i percorsi > 260 caratteri falliscono (MAX_PATH).
        if (!CreateHardLinkW(LongPath.Extended(linkPath), LongPath.Extended(targetPath), IntPtr.Zero))
        {
            var err = Marshal.GetLastWin32Error();
            // HRESULT valorizzato col codice Win32 (0x8007xxxx), cosi' DiskError puo' riconoscere
            // un errore di settore danneggiato (CRC, ecc.) e darne un avviso comprensibile.
            throw new IOException($"CreateHardLink fallito (Win32 {err}): {linkPath} -> {targetPath}",
                unchecked((int)(0x80070000u | (uint)err)));
        }
    }

    /// <summary>Come <see cref="Create"/> ma ritorna false invece di lanciare.</summary>
    public static bool TryCreate(string linkPath, string targetPath)
        => CreateHardLinkW(LongPath.Extended(linkPath), LongPath.Extended(targetPath), IntPtr.Zero);
}
