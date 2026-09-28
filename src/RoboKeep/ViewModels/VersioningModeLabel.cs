using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RoboKeep.Core.Services;
using RoboKeep.Localization;

namespace RoboKeep.ViewModels;

/// <summary>
/// La riga di stato sotto «Tieni le versioni»: quale dei due modelli userà il job con la
/// destinazione scelta. Si calcola guardando il disco (<see cref="VersioningLayout.Detect"/>
/// scrive un file di prova quando non c'è ancora un layout), quindi mai sul thread della UI e mai
/// su una cartella che non esiste — su una destinazione appena digitata a metà si direbbe di no a
/// ogni tasto premuto, scrivendo file di prova in cartelle a caso.
/// </summary>
public static class VersioningModeLabel
{
    /// <summary>Ritardo prima di interrogare il disco: la destinazione si scrive un carattere alla
    /// volta, e ogni carattere farebbe partire una prova.</summary>
    public static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(600);

    /// <summary>Testo per la destinazione indicata. Tocca il disco: da chiamare in
    /// <c>Task.Run</c>.</summary>
    public static string For(string? destination)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(destination) || !Path.IsPathFullyQualified(destination.Trim()))
                return Loc.Instance["Ver_ModeUnknown"];
            // La cartella di destinazione di solito non esiste ancora quando la si scrive nel
            // wizard: gli hard-link sono una proprieta' del volume, quindi la prova si fa nella
            // prima cartella esistente risalendo verso la radice (al limite la radice stessa).
            var probeDir = NearestExistingDirectory(destination.Trim());
            if (probeDir is null) return Loc.Instance["Ver_ModeUnknown"];
            var mode = VersioningLayout.Detect(destination.Trim(), _ => HardLinkSupport.IsSupported(probeDir));
            return mode == VersioningMode.HardLinks
                ? Loc.Instance["Ver_ModeHardLinks"]
                : Loc.Instance["Ver_ModeDifferential"];
        }
        catch
        {
            // Destinazione irraggiungibile o sparita: non si sa, e non lo si inventa.
            return Loc.Instance["Ver_ModeUnknown"];
        }
    }

    /// <summary>La cartella indicata se esiste, altrimenti il primo antenato esistente; null se
    /// nemmeno la radice c'e' (disco scollegato o lettera inesistente).</summary>
    private static string? NearestExistingDirectory(string path)
    {
        var dir = path;
        while (!string.IsNullOrEmpty(dir))
        {
            if (Directory.Exists(dir)) return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    /// <summary>Calcola il testo dopo il <see cref="Debounce"/> e lo consegna a
    /// <paramref name="apply"/>, a meno che nel frattempo la destinazione non sia cambiata di nuovo
    /// (l'attesa viene annullata). Il risultato torna sul thread da cui è stata chiamata.</summary>
    public static async Task RefreshAsync(string? destination, Action<string> apply, CancellationToken ct)
    {
        try
        {
            await Task.Delay(Debounce, ct);
            var text = await Task.Run(() => For(destination), ct);
            if (!ct.IsCancellationRequested) apply(text);
        }
        catch (OperationCanceledException) { /* digitazione proseguita: decide il calcolo successivo */ }
        // La sorgente di annullamento viene liberata appena un calcolo più recente la sostituisce:
        // chi era in attesa su di lei si ritrova il token già smaltito, e non è un errore.
        catch (ObjectDisposedException) { }
    }
}
