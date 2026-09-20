using System.ComponentModel;
using System.Text.RegularExpressions;

namespace RoboKeep.Core.Services;

/// <summary>
/// Riconosce gli errori tipici di un disco (o del suo collegamento) che sta cedendo, per
/// trasformare messaggi criptici ("controllo di ridondanza ciclico") in avvisi comprensibili
/// e, soprattutto, per FERMARE il lavoro: continuare a leggere/scrivere su un supporto che
/// segnala errori hardware peggiora il danno.
/// </summary>
public static class DiskError
{
    /// <summary>true se l'eccezione indica un errore a livello hardware: CRC (Win32 23,
    /// "errore nei dati / controllo di ridondanza ciclico"), settore non trovato (27) o errore
    /// del dispositivo I/O (1117). Sono i segnali classici di settori danneggiati, ma anche di
    /// cavo/box USB/alimentazione difettosi. Esamina anche le eccezioni interne: chi incapsula
    /// (es. <see cref="FileSystemDelete"/> con una <see cref="Win32Exception"/>) non deve
    /// nascondere la causa.</summary>
    public static bool IsUnreadable(System.Exception? ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is DiskHardwareException) return true;
            if (e is Win32Exception w && IsHardwareCode(w.NativeErrorCode)) return true;
            // L'HRESULT di un errore Win32 ha la forma 0x8007xxxx, dove xxxx e' il codice Windows.
            if ((e.HResult & unchecked((int)0xFFFF0000)) == unchecked((int)0x80070000)
                && IsHardwareCode(e.HResult & 0xFFFF))
                return true;
        }
        return false;
    }

    private static bool IsHardwareCode(int win32) => win32 is 23 or 27 or 1117;

    // Riga di errore di robocopy: "<data> ERRORE 23 (0x00000017) Copia del file in corso <percorso>".
    // La parola "ERRORE" e' localizzata, il codice decimale seguito dall'esadecimale tra parentesi no:
    // ci si aggancia a quello, pretendendo che decimale ed esadecimale coincidano.
    private static readonly Regex RobocopyLine = new(
        @"\s(?<dec>23|27|1117)\s+\(0x(?<hex>[0-9A-Fa-f]{8})\)\s*(?<rest>.*)$", RegexOptions.Compiled);

    /// <summary>true se <paramref name="line"/> e' una riga di errore robocopy con un codice
    /// hardware (23, 27, 1117). <paramref name="detail"/> riceve il resto della riga (azione e
    /// percorso, nella lingua di robocopy) per mostrarlo all'utente.</summary>
    public static bool IsRobocopyHardwareErrorLine(string? line, out string detail)
    {
        detail = "";
        if (string.IsNullOrEmpty(line)) return false;
        var m = RobocopyLine.Match(line);
        if (!m.Success) return false;
        if (!int.TryParse(m.Groups["dec"].Value, out var dec)) return false;
        if (System.Convert.ToInt32(m.Groups["hex"].Value, 16) != dec) return false;
        detail = $"Win32 {dec}: {m.Groups["rest"].Value.Trim()}";
        return true;
    }
}

/// <summary>Un disco (o il suo collegamento) ha segnalato un errore hardware: il lavoro in corso
/// va interrotto, non aggirato.</summary>
public sealed class DiskHardwareException : IOException
{
    /// <summary>Percorso su cui si e' manifestato l'errore.</summary>
    public string FaultPath { get; }

    public DiskHardwareException(string faultPath, System.Exception inner)
        : base($"{faultPath}: {inner.Message}", inner)
    {
        FaultPath = faultPath;
    }
}
