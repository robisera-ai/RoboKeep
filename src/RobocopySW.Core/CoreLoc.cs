using System.Globalization;

namespace RobocopySW.Core;

/// <summary>
/// Localizzazione minimale per i testi generati da Core (riepiloghi, esiti), basata sulla
/// cultura UI corrente del thread (impostata dall'app al cambio lingua).
/// </summary>
internal static class CoreLoc
{
    private static bool IsItalian =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "it";

    /// <summary>Restituisce la versione italiana o inglese a seconda della lingua corrente.</summary>
    public static string S(string it, string en) => IsItalian ? it : en;
}
