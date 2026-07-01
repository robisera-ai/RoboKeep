namespace RoboKeep.Core.Services;

/// <summary>
/// Converte un percorso nella forma "extended-length" (prefisso <c>\\?\</c>) per aggirare il limite
/// storico MAX_PATH (260 caratteri) delle chiamate Win32 dirette come CreateHardLinkW e CreateFileW:
/// senza il prefisso, un percorso più lungo fallisce con ERROR_PATH_NOT_FOUND (Win32 3). Le API .NET
/// gestiscono già i percorsi lunghi; le P/Invoke grezze no, quindi vanno prefissate a mano.
/// </summary>
public static class LongPath
{
    public static string Extended(string path)
    {
        if (string.IsNullOrEmpty(path) || path.StartsWith(@"\\?\", StringComparison.Ordinal))
            return path;

        var full = Path.GetFullPath(path); // normalizza a backslash e risolve . / .. (richiesto da \\?\)

        // UNC: \\server\share  ->  \\?\UNC\server\share
        if (full.StartsWith(@"\\", StringComparison.Ordinal))
            return @"\\?\UNC\" + full[2..];

        return @"\\?\" + full;
    }
}
