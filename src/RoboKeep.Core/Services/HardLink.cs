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
            throw new IOException($"CreateHardLink fallito (Win32 {Marshal.GetLastWin32Error()}): {linkPath} -> {targetPath}");
    }

    /// <summary>Come <see cref="Create"/> ma ritorna false invece di lanciare.</summary>
    public static bool TryCreate(string linkPath, string targetPath)
        => CreateHardLinkW(LongPath.Extended(linkPath), LongPath.Extended(targetPath), IntPtr.Zero);
}
