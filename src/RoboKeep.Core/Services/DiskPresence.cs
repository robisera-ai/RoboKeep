using System.Diagnostics.Eventing.Reader;

namespace RoboKeep.Core.Services;

/// <summary>
/// Un collegamento (o scollegamento) di un disco fisico, come lo registra Windows nel registro
/// «Microsoft-Windows-Partition/Diagnostic», evento 1006: il numero che il disco aveva in quel
/// momento e la sua identita'.
/// <para>Serve ad attribuire gli errori del registro Sistema al disco giusto. Quegli errori nominano
/// il disco per numero (<c>\Device\Harddisk1</c>) o per lettera (<c>E:</c>), e con la rotazione dei
/// dischi numero e lettera passano da un disco all'altro: senza questo elenco gli errori di un disco
/// venivano mostrati per quello collegato dopo.</para>
/// <para>Identita': prima la firma scritta SUL disco (MBR) o il suo GUID (GPT), che non dipendono
/// dal case USB; poi produttore, modello e numero di serie, che via USB possono essere quelli del
/// case (un numero di serie come 1234567890123 e' del ponte USB, non del disco).</para>
/// </summary>
/// <param name="Present">false = scollegato (Windows lo registra con capacita' zero).</param>
public sealed record DiskPresence(DateTime When, int DiskNumber, bool Present, string? OnDiskId, string? HardwareId)
{
    /// <summary>Margine fra l'errore e il collegamento registrato: Windows puo' scrivere l'errore
    /// un istante prima del collegamento dello stesso disco.</summary>
    private static readonly TimeSpan Tolerance = TimeSpan.FromSeconds(2);

    public static DiskPresence From(DateTime when, int diskNumber, long capacity, string? partitionTable,
        string? manufacturer, string? model, string? serial)
    {
        var present = capacity > 0;
        var parts = new[] { manufacturer, model, serial }.Select(s => (s ?? "").Trim()).ToArray();
        var hardware = parts.Any(p => p.Length > 0) ? string.Join("|", parts) : null;
        return new DiskPresence(when, diskNumber, present,
            present ? OnDiskIdFrom(partitionTable) : null,
            present ? hardware : null);
    }

    /// <summary>Firma MBR o GUID GPT dalla tabella delle partizioni che Windows registra in
    /// esadecimale (DRIVE_LAYOUT_INFORMATION_EX): stile (4 byte: 0 = MBR, 1 = GPT), numero di voci
    /// (4 byte), poi la firma (MBR, 4 byte) o il GUID del disco (GPT, 16 byte). null se manca o non
    /// si legge.</summary>
    public static string? OnDiskIdFrom(string? partitionTable)
    {
        if (string.IsNullOrWhiteSpace(partitionTable) || partitionTable.Length < 24) return null;
        byte[] bytes;
        try { bytes = Convert.FromHexString(partitionTable.Length % 2 == 0 ? partitionTable : partitionTable[..^1]); }
        catch (FormatException) { return null; }

        switch (BitConverter.ToInt32(bytes, 0))
        {
            case 0:
                var signature = BitConverter.ToUInt32(bytes, 8);
                return signature == 0 ? null : $"MBR:{signature:X8}";
            case 1 when bytes.Length >= 24:
                var guid = new Guid(bytes.AsSpan(8, 16));
                return guid == Guid.Empty ? null : $"GPT:{guid}";
            default:
                return null;
        }
    }

    /// <summary>true se un errore registrato alle <paramref name="eventTime"/> per il disco numero
    /// <paramref name="diskNumber"/> riguarda il disco che ha quel numero ADESSO. Se non si sa
    /// (elenco vuoto, errore piu' vecchio del primo collegamento registrato, identita' non
    /// confrontabili) risponde true: meglio un avviso in piu' che un avviso perso.</summary>
    public static bool BelongsToCurrentDisk(DateTime eventTime, int diskNumber, IReadOnlyList<DiskPresence> timeline)
    {
        var mine = timeline.Where(p => p.DiskNumber == diskNumber && p.Present).OrderBy(p => p.When).ToList();
        var current = mine.LastOrDefault();
        // Il disco presente a quel numero al momento dell'errore (o appena scollegato: chi sparisce
        // a I/O in corso lascia gli errori e poi lo scollegamento).
        var then = mine.LastOrDefault(p => p.When <= eventTime + Tolerance);
        if (current is null || then is null) return true;
        return SameDisk(then, current) ?? true;
    }

    private static bool? SameDisk(DiskPresence a, DiskPresence b)
    {
        if (a.OnDiskId is not null && b.OnDiskId is not null)
            return string.Equals(a.OnDiskId, b.OnDiskId, StringComparison.OrdinalIgnoreCase);
        if (a.HardwareId is not null && b.HardwareId is not null)
            return string.Equals(a.HardwareId, b.HardwareId, StringComparison.OrdinalIgnoreCase);
        return null;
    }

    /// <summary>I collegamenti registrati da Windows, dal piu' vecchio che il registro conserva.
    /// Best-effort: registro assente, disattivato o illeggibile = elenco vuoto (e allora ogni errore
    /// si conta, come prima).</summary>
    public static IReadOnlyList<DiskPresence> ReadTimeline()
    {
        var list = new List<DiskPresence>();
        try
        {
            var query = new EventLogQuery("Microsoft-Windows-Partition/Diagnostic", PathType.LogName,
                "*[System[(EventID=1006)]]");
            using var reader = new EventLogReader(query);
            for (var ev = reader.ReadEvent(); ev is not null; ev = reader.ReadEvent())
            {
                using (ev)
                {
                    if (ev.TimeCreated is not DateTime when) continue;
                    var data = EventData(ev);
                    if (!int.TryParse(data.GetValueOrDefault("DiskNumber"), out var number)) continue;
                    long.TryParse(data.GetValueOrDefault("Capacity"), out var capacity);
                    list.Add(From(when, number, capacity, data.GetValueOrDefault("PartitionTable"),
                        data.GetValueOrDefault("Manufacturer"), data.GetValueOrDefault("Model"),
                        data.GetValueOrDefault("SerialNumber")));
                }
            }
        }
        catch
        {
            // Registro non disponibile: si torna al comportamento di prima.
        }
        return list;
    }

    private static Dictionary<string, string> EventData(EventRecord ev)
    {
        var data = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            var xml = System.Xml.Linq.XDocument.Parse(ev.ToXml());
            foreach (var d in xml.Descendants().Where(e => e.Name.LocalName == "Data"))
                if (d.Attribute("Name")?.Value is { } name)
                    data[name] = d.Value;
        }
        catch (System.Xml.XmlException) { /* evento malformato: nessun dato */ }
        return data;
    }
}
