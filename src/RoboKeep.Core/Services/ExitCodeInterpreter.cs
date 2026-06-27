namespace RoboKeep.Core.Services;

/// <summary>Esito interpretato di un exit code robocopy.</summary>
/// <param name="Code">Exit code grezzo.</param>
/// <param name="Success">true se l'esecuzione è riuscita (nessun bit di errore).</param>
/// <param name="Summary">Riga di sintesi leggibile.</param>
/// <param name="Details">Dettaglio per ciascun bit significativo.</param>
public sealed record ExitCodeResult(int Code, bool Success, string Summary, IReadOnlyList<string> Details);

/// <summary>
/// Interpreta gli exit code di robocopy, che sono una maschera di bit:
/// 1=file copiati, 2=elementi extra in destinazione, 4=mismatch,
/// 8=errori di copia, 16=errore grave. Successo = nessun bit 8/16 (code &lt; 8).
/// Replica la logica usata storicamente da <c>vbs/sendmail.vbs</c>.
/// </summary>
public static class ExitCodeInterpreter
{
    private const int FilesCopied = 1;
    private const int ExtraItems = 2;
    private const int Mismatch = 4;
    private const int CopyErrors = 8;
    private const int FatalError = 16;

    public static ExitCodeResult Interpret(int code)
    {
        // Un codice negativo non è un esito robocopy valido: trattalo come errore.
        if (code < 0)
            return new ExitCodeResult(code, false,
                CoreLoc.S("Exit_InvalidSummary"),
                new[] { CoreLoc.S("Exit_InvalidDetail") });

        var details = new List<string>();

        if ((code & FilesCopied) != 0)
            details.Add(CoreLoc.S("Exit_Copied"));
        if ((code & ExtraItems) != 0)
            details.Add(CoreLoc.S("Exit_Extra"));
        if ((code & Mismatch) != 0)
            details.Add(CoreLoc.S("Exit_Mismatch"));
        if ((code & CopyErrors) != 0)
            details.Add(CoreLoc.S("Exit_Errors"));
        if ((code & FatalError) != 0)
            details.Add(CoreLoc.S("Exit_Fatal"));

        var success = (code & (CopyErrors | FatalError)) == 0;

        string summary;
        if (code == 0)
            summary = CoreLoc.S("Exit_NoChange");
        else if (success)
            summary = CoreLoc.S("Exit_Success");
        else
            summary = CoreLoc.S("Exit_WithErrors");

        if (details.Count == 0)
            details.Add(summary);

        return new ExitCodeResult(code, success, summary, details);
    }
}
