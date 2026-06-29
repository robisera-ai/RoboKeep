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
}
