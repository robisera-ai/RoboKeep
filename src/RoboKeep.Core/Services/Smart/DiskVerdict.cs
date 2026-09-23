namespace RoboKeep.Core.Services.Smart;

public enum DiskHealthLevel { Good, Warning, Danger, Unreadable }

/// <summary>Una riga del referto: chiave di localizzazione (etichetta + spiegazione stanno in
/// Loc.cs come <c>Key</c> e <c>Key_Hint</c>), valore da mostrare, gravita' della riga.</summary>
public sealed record DiskFinding(string Key, string Value, DiskHealthLevel Level);

/// <summary>Verdetto complessivo + righe.</summary>
public sealed record DiskVerdictResult(DiskHealthLevel Level, IReadOnlyList<DiskFinding> Findings);

/// <summary>
/// Regole del verdetto, pure e dichiarate nella spec. Un settore riallocato o non correggibile
/// e' un disco che cede (Pericolo); un settore in attesa e' spesso una scrittura interrotta che
/// una riscrittura sistema (Attenzione); gli errori CRC (C7) riguardano il collegamento box-disco
/// e non il disco: sono una nota, non un verdetto.
/// </summary>
public static class DiskVerdict
{
    public const int HddHotC = 50;
    public const int NvmeHotC = 70;
    public const int NvmeWornPercent = 90;

    public static DiskVerdictResult Evaluate(SmartAttributes s)
    {
        var rows = new List<DiskFinding>();
        var level = DiskHealthLevel.Good;
        void Row(byte id, string key, Func<long, DiskHealthLevel> judge)
        {
            if (!s.TryGet(id, out var a)) return;
            rows.Add(new DiskFinding(key, a.Raw.ToString(), judge(a.Raw)));
        }
        DiskHealthLevel Raise(DiskHealthLevel l) { if (l > level) level = l; return l; }

        Row(0x05, "Smart_Reallocated", r => Raise(r > 0 ? DiskHealthLevel.Danger : DiskHealthLevel.Good));
        Row(0xC5, "Smart_Pending", r => Raise(r > 0 ? DiskHealthLevel.Warning : DiskHealthLevel.Good));
        Row(0xC6, "Smart_Uncorrectable", r => Raise(r > 0 ? DiskHealthLevel.Danger : DiskHealthLevel.Good));
        // C7: nota sul collegamento, NON alza il verdetto del disco. Si mostra solo quando c'e'
        // qualcosa da dire: su un disco sano (C7 = 0) la riga sarebbe rumore.
        if (s.TryGet(0xC7, out var crc) && crc.Raw > 0)
            rows.Add(new DiskFinding("Smart_LinkErrors", crc.Raw.ToString(), DiskHealthLevel.Warning));
        // BB: storico cumulativo, informativo.
        Row(0xBB, "Smart_ReportedUncorrectable", _ => DiskHealthLevel.Good);
        if (s.TemperatureC is int t)
            rows.Add(new DiskFinding("Smart_Temperature", $"{t} °C", Raise(t > HddHotC ? DiskHealthLevel.Warning : DiskHealthLevel.Good)));
        if (s.PowerOnHours is long h)
            rows.Add(new DiskFinding("Smart_PowerOnHours", h.ToString(), DiskHealthLevel.Good));
        return new DiskVerdictResult(level, rows);
    }

    public static DiskVerdictResult Evaluate(NvmeHealth h)
    {
        var rows = new List<DiskFinding>();
        var level = DiskHealthLevel.Good;
        DiskHealthLevel Add(string key, string value, DiskHealthLevel l) { rows.Add(new DiskFinding(key, value, l)); if (l > level) level = l; return l; }

        Add("Smart_NvmeCritical", h.CriticalWarning == 0 ? "0" : $"0x{h.CriticalWarning:X2}", h.CriticalWarning != 0 ? DiskHealthLevel.Danger : DiskHealthLevel.Good);
        // Valore neutro «disponibile / soglia»: il senso lo dà la spiegazione tradotta, non il testo
        // del valore, che altrimenti resterebbe in italiano nelle altre lingue.
        Add("Smart_NvmeSpare", $"{h.AvailableSpare} % / {h.SpareThreshold} %", h.AvailableSpare < h.SpareThreshold ? DiskHealthLevel.Danger : DiskHealthLevel.Good);
        Add("Smart_NvmeMediaErrors", h.MediaErrors.ToString(), h.MediaErrors > 0 ? DiskHealthLevel.Danger : DiskHealthLevel.Good);
        Add("Smart_NvmeUsed", $"{h.PercentUsed} %", h.PercentUsed >= NvmeWornPercent ? DiskHealthLevel.Warning : DiskHealthLevel.Good);
        Add("Smart_Temperature", $"{h.TemperatureC} °C", h.TemperatureC > NvmeHotC ? DiskHealthLevel.Warning : DiskHealthLevel.Good);
        Add("Smart_NvmeUnsafeShutdowns", h.UnsafeShutdowns.ToString(), DiskHealthLevel.Good);
        Add("Smart_PowerOnHours", h.PowerOnHours.ToString(), DiskHealthLevel.Good);
        return new DiskVerdictResult(level, rows);
    }
}
