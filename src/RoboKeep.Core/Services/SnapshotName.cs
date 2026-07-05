using System.Globalization;

namespace RoboKeep.Core.Services;

/// <summary>Convenzioni di naming degli snapshot datati (cartelle in destinazione).</summary>
public static class SnapshotName
{
    public const string Format = "yyyy-MM-dd_HHmmss";
    public const string InProgressSuffix = ".inprogress";

    public static string For(DateTime now) => now.ToString(Format, CultureInfo.InvariantCulture);

    public static bool IsInProgress(string name)
        => name.EndsWith(InProgressSuffix, StringComparison.OrdinalIgnoreCase);

    public static bool TryParse(string name, out DateTime date)
        => DateTime.TryParseExact(name, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    /// <summary>Nomi degli snapshot validi (niente .inprogress, nome parsabile) in una
    /// destinazione, in ordine cronologico inverso (più recente prima). Cartella mancante → vuoto.</summary>
    public static IReadOnlyList<string> ListValid(string destinationDir)
    {
        if (!Directory.Exists(destinationDir)) return Array.Empty<string>();
        return Directory.GetDirectories(destinationDir)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(n => !IsInProgress(n) && TryParse(n, out _))
            .OrderByDescending(n => { TryParse(n, out var d); return d; })
            .ToList();
    }

    /// <summary>Nome dello snapshot più recente, o null se non ce ne sono.</summary>
    public static string? Latest(string destinationDir) => ListValid(destinationDir).FirstOrDefault();
}
