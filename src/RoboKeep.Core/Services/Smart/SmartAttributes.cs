namespace RoboKeep.Core.Services.Smart;

/// <summary>Un attributo SMART ATA: id, valore normalizzato, peggiore storico, valore grezzo (48 bit).</summary>
public sealed record SmartAttribute(byte Id, byte Value, byte Worst, long Raw);

/// <summary>Blocco SMART ATA gia' decodificato, per id. Puro: nessun accesso all'hardware.</summary>
public sealed class SmartAttributes
{
    private readonly Dictionary<byte, SmartAttribute> _byId;
    private SmartAttributes(Dictionary<byte, SmartAttribute> byId) => _byId = byId;

    public int Count => _byId.Count;
    public SmartAttribute this[byte id] => _byId[id];
    public bool TryGet(byte id, out SmartAttribute attr) => _byId.TryGetValue(id, out attr!);
    public IReadOnlyCollection<SmartAttribute> All => _byId.Values;

    /// <summary>Temperatura in gradi (attributo C2 o BE), o null. La temperatura corrente sta nel
    /// byte piu' basso del raw: i byte superiori portano minimo e massimo registrati dal produttore,
    /// e leggerli insieme darebbe numeri assurdi. Fuori da 0..120 °C non e' una temperatura:
    /// l'attributo ha un altro significato su quel disco, meglio non mostrarlo.</summary>
    public int? TemperatureC =>
        TryGet(0xC2, out var t) ? Celsius(t.Raw) : TryGet(0xBE, out var a) ? Celsius(a.Raw) : null;

    private static int? Celsius(long raw)
    {
        var c = (int)(raw & 0xFF);
        return c is >= 0 and <= 120 ? c : null;
    }

    /// <summary>Ore di accensione (attributo 09), o null.</summary>
    public long? PowerOnHours => TryGet(0x09, out var p) ? p.Raw & 0xFFFFFFFF : null;

    /// <summary>Decodifica il blocco da 512 byte di SMART READ DATA: 2 byte di versione, poi 30
    /// voci da 12 byte (id, flag x2, value, worst, raw x6, riservato). Null se non e' un blocco
    /// valido o non contiene attributi.</summary>
    public static SmartAttributes? ParseAta(byte[] block)
    {
        if (block is null || block.Length < 362) return null;
        var byId = new Dictionary<byte, SmartAttribute>();
        for (var i = 0; i < 30; i++)
        {
            var o = 2 + i * 12;
            var id = block[o];
            if (id == 0) continue;
            long raw = 0;
            for (var k = 5; k >= 0; k--) raw = (raw << 8) | block[o + 5 + k];
            byId[id] = new SmartAttribute(id, block[o + 3], block[o + 4], raw);
        }
        return byId.Count == 0 ? null : new SmartAttributes(byId);
    }

    /// <summary>Decodifica il log SMART/Health NVMe (log page 2, 512 byte). Null se troppo corto.</summary>
    public static NvmeHealth? ParseNvme(byte[] log)
    {
        if (log is null || log.Length < 512) return null;
        var kelvin = log[1] | (log[2] << 8);
        return new NvmeHealth(
            CriticalWarning: log[0],
            TemperatureC: kelvin - 273,
            AvailableSpare: log[3],
            SpareThreshold: log[4],
            PercentUsed: log[5],
            PowerOnHours: BitConverter.ToUInt64(log, 128),
            UnsafeShutdowns: BitConverter.ToUInt64(log, 144),
            MediaErrors: BitConverter.ToUInt64(log, 160));
    }
}

/// <summary>Salute di un disco NVMe (dal log page 2).</summary>
public sealed record NvmeHealth(
    byte CriticalWarning, int TemperatureC, byte AvailableSpare, byte SpareThreshold, byte PercentUsed,
    ulong PowerOnHours, ulong UnsafeShutdowns, ulong MediaErrors);
