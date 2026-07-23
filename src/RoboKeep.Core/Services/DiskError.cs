namespace RoboKeep.Core.Services;

/// <summary>
/// Riconosce gli errori tipici di un disco che sta cedendo, per trasformare messaggi criptici
/// ("controllo di ridondanza ciclico") in avvisi comprensibili e azionabili.
/// </summary>
public static class DiskError
{
    /// <summary>true se l'eccezione indica un settore illeggibile a livello hardware: CRC
    /// (Win32 23, "errore nei dati / controllo di ridondanza ciclico"), settore non trovato (27)
    /// o errore del dispositivo I/O (1117). Sono i segnali classici di settori danneggiati.</summary>
    public static bool IsUnreadable(System.Exception ex)
    {
        // L'HRESULT di un errore Win32 ha la forma 0x8007xxxx, dove xxxx e' il codice Windows.
        if ((ex.HResult & unchecked((int)0xFFFF0000)) != unchecked((int)0x80070000)) return false;
        return (ex.HResult & 0xFFFF) is 23 or 27 or 1117;
    }
}
