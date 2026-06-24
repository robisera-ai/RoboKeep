namespace RobocopySW.Core.Services;

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
                CoreLoc.S("Errore: codice di uscita non valido.", "Error: invalid exit code."),
                new[] { CoreLoc.S("Codice di uscita negativo/non valido.", "Negative/invalid exit code.") });

        var details = new List<string>();

        if ((code & FilesCopied) != 0)
            details.Add(CoreLoc.S("File e/o cartelle copiati correttamente.", "Files and/or folders copied successfully."));
        if ((code & ExtraItems) != 0)
            details.Add(CoreLoc.S("Rilevati elementi extra in destinazione (in mirror vengono rimossi).", "Extra items detected at the destination (removed in mirror mode)."));
        if ((code & Mismatch) != 0)
            details.Add(CoreLoc.S("Rilevati elementi non corrispondenti (mismatch): verifica consigliata.", "Mismatched items detected: review recommended."));
        if ((code & CopyErrors) != 0)
            details.Add(CoreLoc.S("Errori durante la copia di alcuni file (tentativi esauriti).", "Errors copying some files (retries exhausted)."));
        if ((code & FatalError) != 0)
            details.Add(CoreLoc.S("Errore grave: robocopy non ha potuto copiare nulla.", "Serious error: robocopy could not copy anything."));

        var success = (code & (CopyErrors | FatalError)) == 0;

        string summary;
        if (code == 0)
            summary = CoreLoc.S("Nessuna modifica necessaria: sorgente e destinazione già allineate.", "No changes needed: source and destination already in sync.");
        else if (success)
            summary = CoreLoc.S("Completato con successo.", "Completed successfully.");
        else
            summary = CoreLoc.S("Completato con errori.", "Completed with errors.");

        if (details.Count == 0)
            details.Add(summary);

        return new ExitCodeResult(code, success, summary, details);
    }
}
