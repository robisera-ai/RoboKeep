using System.IO.Compression;
using RobocopySW.Core.Models;

namespace RobocopySW.Core.Services;

/// <summary>
/// Gestisce i log dei job: scrittura, compressione in .zip archiviata per data
/// (<c>logRoot\AAAAMMGG\AAAAMMGG-HHMMSS-&lt;job&gt;.log[.zip]</c>) e pulizia dei vecchi log.
/// </summary>
public sealed class LogService
{
    private readonly AppSettings _settings;

    public LogService(AppSettings settings) => _settings = settings;

    private string LogRoot =>
        string.IsNullOrWhiteSpace(_settings.LogRoot)
            ? Path.Combine(AppContext.BaseDirectory, "logs")
            : _settings.LogRoot;

    private string TempRoot =>
        string.IsNullOrWhiteSpace(_settings.TempRoot)
            ? Path.Combine(Path.GetTempPath(), "RobocopySW")
            : _settings.TempRoot;

    /// <summary>Rimuove dal nome i caratteri non validi per un file.</summary>
    public static string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = (name ?? "").Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var result = new string(chars).Trim();
        return string.IsNullOrEmpty(result) ? "job" : result;
    }

    /// <summary>Nome file del log: <c>AAAAMMGG-HHMMSS-&lt;job&gt;.log</c>.</summary>
    public static string BuildLogFileName(string jobName, DateTime timestamp) =>
        $"{timestamp:yyyyMMdd-HHmmss}-{SanitizeName(jobName)}.log";

    /// <summary>Cartella giornaliera di archiviazione: <c>AAAAMMGG</c>.</summary>
    public static string DailyFolderName(DateTime timestamp) => timestamp.ToString("yyyyMMdd");

    /// <summary>
    /// Scrive il contenuto del log e lo archivia (zip se abilitato) sotto la cartella del giorno.
    /// Restituisce il percorso finale del log (.log o .log.zip).
    /// </summary>
    public string WriteAndArchive(string jobName, string content, DateTime timestamp)
    {
        var fileName = BuildLogFileName(jobName, timestamp);
        var dailyDir = Path.Combine(LogRoot, DailyFolderName(timestamp));
        Directory.CreateDirectory(dailyDir);

        if (!_settings.CompressLogs)
        {
            var logPath = Path.Combine(dailyDir, fileName);
            File.WriteAllText(logPath, content);
            return logPath;
        }

        // Scrive prima in temp, poi comprime nello zip di destinazione.
        Directory.CreateDirectory(TempRoot);
        var tempLog = Path.Combine(TempRoot, fileName);
        File.WriteAllText(tempLog, content);

        var zipPath = Path.Combine(dailyDir, fileName + ".zip");
        if (File.Exists(zipPath))
            File.Delete(zipPath);

        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            zip.CreateEntryFromFile(tempLog, fileName, CompressionLevel.Optimal);

        File.Delete(tempLog);
        return zipPath;
    }

    /// <summary>
    /// Elimina i file di log più vecchi di <see cref="AppSettings.LogRetentionDays"/> giorni.
    /// Restituisce il numero di file eliminati. Se la ritenzione è 0 non fa nulla.
    /// </summary>
    public int CleanupOldLogs(DateTime now)
    {
        var days = _settings.LogRetentionDays;
        if (days <= 0 || !Directory.Exists(LogRoot))
            return 0;

        var cutoff = now.AddDays(-days);
        var deleted = 0;

        foreach (var file in Directory.EnumerateFiles(LogRoot, "*", SearchOption.AllDirectories))
        {
            if (File.GetLastWriteTime(file) < cutoff)
            {
                try
                {
                    File.Delete(file);
                    deleted++;
                }
                catch
                {
                    // best-effort: un file bloccato non interrompe la pulizia.
                }
            }
        }

        // Rimuove le cartelle giornaliere rimaste vuote.
        foreach (var dir in Directory.EnumerateDirectories(LogRoot))
        {
            if (!Directory.EnumerateFileSystemEntries(dir).Any())
            {
                try { Directory.Delete(dir); } catch { /* ignora */ }
            }
        }

        return deleted;
    }
}
