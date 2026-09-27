using System.Text.RegularExpressions;

namespace RoboKeep.Core.Services;

/// <summary>
/// Riconosce nell'output di robocopy lo «spazio su disco insufficiente» (Win32 112). Non e' un
/// guasto - il disco sta benissimo, e' pieno - e per questo NON passa da <see cref="DiskError"/>:
/// il rimedio non e' controllare cavo e SMART, e' fare posto. Serve a trasformare un exit code in
/// una frase che dice cosa succede e cosa fare.
/// <para>Indipendente dalla lingua: la parola ERRORE/ERROR/FEHLER e' tradotta, il codice decimale
/// seguito dal suo esadecimale no. La forma inglese senza esadecimale si accetta comunque.</para>
/// </summary>
public static class DiskFullDetector
{
    // "2026/09/27 21:30:00 ERRORE 112 (0x00000070) Copia del file in corso E:\Backup\f.dat"
    private static readonly Regex Coded = new(@"\s112\s+\(0x00000070\)", RegexOptions.Compiled);

    // Forma senza esadecimale: maiuscolo esatto (la parola di robocopy in inglese e' ERROR, mentre
    // "error 112" minuscolo compare nei nomi di file veri) e subito dopo il codice solo uno spazio,
    // un due punti o la fine della riga — cosi' un percorso tipo "error 112 risposta.docx" non passa.
    private static readonly Regex Plain = new(@"\bERROR 112(?=[ :\r\n]|$)", RegexOptions.Compiled);

    /// <summary>true se <paramref name="output"/> contiene una riga di errore "spazio insufficiente".</summary>
    public static bool Matches(string? output) =>
        !string.IsNullOrEmpty(output) && (Coded.IsMatch(output) || Plain.IsMatch(output));
}
