# Salute dischi (SMART) — piano di implementazione

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** una finestra «Salute dischi» che legge lo SMART di ogni disco fisico (NVMe senza elevazione, SATA/USB tramite helper elevato con un solo prompt UAC) e mostra un verdetto in italiano con i valori che contano, come da `docs/superpowers/specs/2026-09-23-salute-dischi-design.md`.

**Architecture:** nel Core, sotto `Services/Smart/`: parse puro dei buffer (`SmartAttributes`, `NvmeHealth`), regole pure del verdetto (`DiskVerdict`), P/Invoke best-effort (`SmartReader`), modello di scambio JSON (`DiskReport`), orchestrazione (`SmartSession`, che avvia `RoboKeep.exe --smart-helper <dir>` con `runas`, come `VssSession`). L'app aggiunge la modalità helper in `App.xaml.cs`/`CliOptions`, la finestra `DiskHealthWindow` con il suo view model, il pulsante nella barra e i testi in 5 lingue.

**Tech Stack:** .NET 10, WPF + WPF-UI 4.3, xUnit, P/Invoke su kernel32 (`CreateFileW`, `DeviceIoControl`).

**Regole del repo:** nessun commit finché l'utente non ha verificato; CRLF (`unix2dos -q`); mai terminare RoboKeep se blocca la build (chiedere di chiuderlo); `Loc.cs` in 5 sezioni con parità (`LocParityTests`); i simboli WPF-UI vanno verificati (`grep -c <Nome> ~/.nuget/packages/wpf-ui/4.3.0/lib/net9.0-windows7.0/Wpf.Ui.dll` > 0). Commenti in italiano; nel Core apostrofi ASCII.

**Fatti verificati sull'hardware** (sonde del 23/09/2026, da rispettare): l'ATA passthrough funziona solo con handle `GENERIC_READ|GENERIC_WRITE` (0xC0000000) su `\\.\PhysicalDriveN` e richiede admin; su un NVMe torna `ScsiStatus=2` con buffer spazzatura → accettare solo `ScsiStatus==0` e azzerare il buffer prima; il log NVMe via `IOCTL_STORAGE_QUERY_PROPERTY` (PropertyId 50, ProtocolTypeNvme=3, NVMeDataTypeLogPage=2, log page 2, offset dati 40, lunghezza 512) funziona senza admin con handle ad accesso 0.

---

## Mappa dei file

| File | Ruolo |
|---|---|
| `src/RoboKeep.Core/Services/Smart/SmartAttributes.cs` (nuovo) | parse ATA (512 byte) e NVMe (log page 2), puri |
| `src/RoboKeep.Core/Services/Smart/DiskVerdict.cs` (nuovo) | regole del verdetto, pure |
| `src/RoboKeep.Core/Services/Smart/DiskReport.cs` (nuovo) | modello + JSON per lo scambio app↔helper |
| `src/RoboKeep.Core/Services/Smart/SmartReader.cs` (nuovo) | P/Invoke: elenco dischi, lettura ATA, lettura NVMe |
| `src/RoboKeep.Core/Services/Smart/SmartHelper.cs` (nuovo) | corpo della modalità `--smart-helper` |
| `src/RoboKeep.Core/Services/Smart/SmartSession.cs` (nuovo) | orchestrazione lato app (NVMe locale + helper elevato) |
| `src/RoboKeep.Tests/SmartAttributesTests.cs`, `DiskVerdictTests.cs`, `DiskReportTests.cs`, `SmartReaderTests.cs` (nuovi) | |
| `src/RoboKeep/CliOptions.cs`, `App.xaml.cs` | `--smart-helper` |
| `src/RoboKeep/DiskHealthWindow.xaml(.cs)`, `ViewModels/DiskHealthViewModel.cs` (nuovi) | finestra |
| `src/RoboKeep/MainWindow.xaml(.cs)` | pulsante «Salute dischi» |
| `src/RoboKeep/Localization/Loc.cs` | testi |
| `docs/guide/it|en/14-*.md`, `16-*.md`, `17-*.md`, `CHANGELOG.md` | documentazione |

---

### Task 1: parse puro — `SmartAttributes` e `NvmeHealth`

**Files:** Create `src/RoboKeep.Core/Services/Smart/SmartAttributes.cs`, `src/RoboKeep.Tests/SmartAttributesTests.cs`.

- [ ] **Step 1: test**

```csharp
using RoboKeep.Core.Services.Smart;

namespace RoboKeep.Tests;

public class SmartAttributesTests
{
    /// <summary>Costruisce un blocco SMART ATA (512 byte) con gli attributi dati: (id, value, worst, raw).</summary>
    internal static byte[] AtaBlock(params (byte Id, byte Value, byte Worst, long Raw)[] attrs)
    {
        var b = new byte[512];
        b[0] = 0x10; b[1] = 0; // versione 16, come sui dischi reali
        for (var i = 0; i < attrs.Length; i++)
        {
            var o = 2 + i * 12;
            b[o] = attrs[i].Id; b[o + 1] = 0x33; b[o + 2] = 0; b[o + 3] = attrs[i].Value; b[o + 4] = attrs[i].Worst;
            var raw = attrs[i].Raw;
            for (var k = 0; k < 6; k++) { b[o + 5 + k] = (byte)(raw & 0xFF); raw >>= 8; }
        }
        return b;
    }

    [Fact]
    public void ParseAta_ReadsIdsValuesAndSixByteRaw()
    {
        // La fotografia del Samsung HD103SI del 23/09/2026: 05=0, 09=387 ore, BB=1138, C5=0, C6=0, C7=9.
        var block = AtaBlock((0x05, 100, 100, 0), (0x09, 100, 100, 387), (0xBB, 100, 100, 1138),
            (0xC2, 71, 62, 488439837), (0xC5, 100, 100, 0), (0xC6, 100, 100, 0), (0xC7, 100, 100, 9));
        var s = SmartAttributes.ParseAta(block)!;
        Assert.Equal(7, s.Count);
        Assert.Equal(387, s[0x09].Raw);
        Assert.Equal(1138, s[0xBB].Raw);
        Assert.Equal(9, s[0xC7].Raw);
        Assert.Equal(71, s[0xC2].Value);
        // La temperatura ATA sta nei due byte bassi del raw (il resto sono min/max del produttore).
        Assert.Equal(29, s[0xC2].Raw & 0xFFFF);
        Assert.Equal(29, s.TemperatureC);
        Assert.Equal(387, s.PowerOnHours);
    }

    [Fact]
    public void ParseAta_RejectsWrongSizeAndEmptyBlock()
    {
        Assert.Null(SmartAttributes.ParseAta(new byte[100]));
        Assert.Null(SmartAttributes.ParseAta(new byte[512])); // nessun attributo: non e' SMART
    }

    [Fact]
    public void ParseAta_RawGreaterThan32Bits()
    {
        var s = SmartAttributes.ParseAta(AtaBlock((0xF1, 100, 100, 0x0123456789AB)))!;
        Assert.Equal(0x0123456789ABL, s[0xF1].Raw);
    }

    [Fact]
    public void ParseNvme_ReadsHealthLog()
    {
        var log = new byte[512];
        log[0] = 0x00;                                      // critical warning
        log[1] = 0x51; log[2] = 0x01;                       // temperatura 337 K = 64 C
        log[3] = 100; log[4] = 10; log[5] = 1;              // spare, soglia, usata
        BitConverter.GetBytes(4UL).CopyTo(log, 144);        // unsafe shutdowns
        BitConverter.GetBytes(0UL).CopyTo(log, 160);        // media errors
        BitConverter.GetBytes(738UL).CopyTo(log, 128);      // power on hours (16 byte, i primi 8 bastano)
        var h = SmartAttributes.ParseNvme(log)!;
        Assert.Equal(0, h.CriticalWarning);
        Assert.Equal(64, h.TemperatureC);
        Assert.Equal(100, h.AvailableSpare);
        Assert.Equal(10, h.SpareThreshold);
        Assert.Equal(1, h.PercentUsed);
        Assert.Equal(4UL, h.UnsafeShutdowns);
        Assert.Equal(0UL, h.MediaErrors);
        Assert.Equal(738UL, h.PowerOnHours);
        Assert.Null(SmartAttributes.ParseNvme(new byte[10]));
    }
}
```

- [ ] **Step 2: implementazione**

```csharp
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

    /// <summary>Temperatura in gradi (attributo C2 o BE), o null.</summary>
    public int? TemperatureC =>
        TryGet(0xC2, out var t) ? (int)(t.Raw & 0xFFFF) : TryGet(0xBE, out var a) ? (int)(a.Raw & 0xFFFF) : null;

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
```

- [ ] **Step 3:** `dotnet test src/RoboKeep.Tests --nologo --filter SmartAttributesTests` → 4 verdi.

---

### Task 2: verdetto puro — `DiskVerdict`

**Files:** Create `src/RoboKeep.Core/Services/Smart/DiskVerdict.cs`, `src/RoboKeep.Tests/DiskVerdictTests.cs`.

- [ ] **Step 1: test**

```csharp
using RoboKeep.Core.Services.Smart;
using static RoboKeep.Tests.SmartAttributesTests;

namespace RoboKeep.Tests;

public class DiskVerdictTests
{
    private static SmartAttributes Ata(params (byte, byte, byte, long)[] a) => SmartAttributes.ParseAta(AtaBlock(a))!;

    [Fact]
    public void HealthyHdd_IsGood()
    {
        var v = DiskVerdict.Evaluate(Ata((0x05, 100, 100, 0), (0xC5, 100, 100, 0), (0xC6, 100, 100, 0), (0xC7, 100, 100, 0), (0xC2, 100, 100, 30)));
        Assert.Equal(DiskHealthLevel.Good, v.Level);
        Assert.DoesNotContain(v.Findings, f => f.Key == "Smart_LinkErrors");
    }

    [Fact]
    public void ReallocatedOrUncorrectable_IsDanger()
    {
        Assert.Equal(DiskHealthLevel.Danger, DiskVerdict.Evaluate(Ata((0x05, 90, 90, 12))).Level);
        Assert.Equal(DiskHealthLevel.Danger, DiskVerdict.Evaluate(Ata((0xC6, 100, 100, 3))).Level);
    }

    [Fact]
    public void PendingSectors_IsWarning_WithTheRewriteAdvice()
    {
        // Il Samsung prima della formattazione completa: C5 = 17, 05 = 0.
        var v = DiskVerdict.Evaluate(Ata((0x05, 100, 100, 0), (0xC5, 100, 100, 17)));
        Assert.Equal(DiskHealthLevel.Warning, v.Level);
        Assert.Contains(v.Findings, f => f.Key == "Smart_Pending" && f.Level == DiskHealthLevel.Warning);
    }

    [Fact]
    public void CrcErrors_AreANote_NotAVerdict()
    {
        var v = DiskVerdict.Evaluate(Ata((0xC7, 100, 100, 9)));
        Assert.Equal(DiskHealthLevel.Good, v.Level);
        Assert.Contains(v.Findings, f => f.Key == "Smart_LinkErrors" && f.Level == DiskHealthLevel.Warning);
    }

    [Fact]
    public void HotHdd_IsWarning()
        => Assert.Equal(DiskHealthLevel.Warning, DiskVerdict.Evaluate(Ata((0xC2, 45, 40, 56))).Level);

    [Fact]
    public void Danger_BeatsWarning()
        => Assert.Equal(DiskHealthLevel.Danger, DiskVerdict.Evaluate(Ata((0x05, 100, 100, 1), (0xC5, 100, 100, 5))).Level);

    [Theory]
    [InlineData(0, 64, 100, 10, 1, 0UL, DiskHealthLevel.Good)]
    [InlineData(1, 64, 100, 10, 1, 0UL, DiskHealthLevel.Danger)]   // avviso critico
    [InlineData(0, 64, 5, 10, 1, 0UL, DiskHealthLevel.Danger)]     // riserva sotto soglia
    [InlineData(0, 64, 100, 10, 1, 3UL, DiskHealthLevel.Danger)]   // errori del supporto
    [InlineData(0, 64, 100, 10, 95, 0UL, DiskHealthLevel.Warning)] // usura
    [InlineData(0, 75, 100, 10, 1, 0UL, DiskHealthLevel.Warning)]  // temperatura
    public void Nvme_Rules(int crit, int temp, int spare, int thr, int used, ulong mediaErr, DiskHealthLevel expected)
    {
        var h = new NvmeHealth((byte)crit, temp, (byte)spare, (byte)thr, (byte)used, 100, 0, mediaErr);
        Assert.Equal(expected, DiskVerdict.Evaluate(h).Level);
    }

    [Fact]
    public void Findings_ListTheValuesThatMatter_InAStableOrder()
    {
        var v = DiskVerdict.Evaluate(Ata((0x05, 100, 100, 0), (0x09, 100, 100, 387), (0xBB, 100, 100, 1138), (0xC2, 71, 62, 29), (0xC5, 100, 100, 0), (0xC6, 100, 100, 0), (0xC7, 100, 100, 9)));
        var keys = v.Findings.Select(f => f.Key).ToList();
        Assert.Equal(new[] { "Smart_Reallocated", "Smart_Pending", "Smart_Uncorrectable", "Smart_LinkErrors", "Smart_ReportedUncorrectable", "Smart_Temperature", "Smart_PowerOnHours" }, keys);
        Assert.Equal("1138", v.Findings.Single(f => f.Key == "Smart_ReportedUncorrectable").Value);
        Assert.Equal("29 °C", v.Findings.Single(f => f.Key == "Smart_Temperature").Value);
    }
}
```

- [ ] **Step 2: implementazione**

```csharp
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
        void Row(byte id, string key, Func<long, DiskHealthLevel> judge, Func<long, string>? fmt = null)
        {
            if (!s.TryGet(id, out var a)) return;
            var l = judge(a.Raw);
            rows.Add(new DiskFinding(key, fmt?.Invoke(a.Raw) ?? a.Raw.ToString(), l));
        }
        DiskHealthLevel Raise(DiskHealthLevel l) { if (l > level) level = l; return l; }

        Row(0x05, "Smart_Reallocated", r => Raise(r > 0 ? DiskHealthLevel.Danger : DiskHealthLevel.Good));
        Row(0xC5, "Smart_Pending", r => Raise(r > 0 ? DiskHealthLevel.Warning : DiskHealthLevel.Good));
        Row(0xC6, "Smart_Uncorrectable", r => Raise(r > 0 ? DiskHealthLevel.Danger : DiskHealthLevel.Good));
        // C7: nota sul collegamento, NON alza il verdetto del disco.
        Row(0xC7, "Smart_LinkErrors", r => r > 0 ? DiskHealthLevel.Warning : DiskHealthLevel.Good);
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
        Add("Smart_NvmeSpare", $"{h.AvailableSpare} % (soglia {h.SpareThreshold} %)", h.AvailableSpare < h.SpareThreshold ? DiskHealthLevel.Danger : DiskHealthLevel.Good);
        Add("Smart_NvmeMediaErrors", h.MediaErrors.ToString(), h.MediaErrors > 0 ? DiskHealthLevel.Danger : DiskHealthLevel.Good);
        Add("Smart_NvmeUsed", $"{h.PercentUsed} %", h.PercentUsed >= NvmeWornPercent ? DiskHealthLevel.Warning : DiskHealthLevel.Good);
        Add("Smart_Temperature", $"{h.TemperatureC} °C", h.TemperatureC > NvmeHotC ? DiskHealthLevel.Warning : DiskHealthLevel.Good);
        Add("Smart_NvmeUnsafeShutdowns", h.UnsafeShutdowns.ToString(), DiskHealthLevel.Good);
        Add("Smart_PowerOnHours", h.PowerOnHours.ToString(), DiskHealthLevel.Good);
        return new DiskVerdictResult(level, rows);
    }
}
```

Nota: l'ordine dell'enum (`Good < Warning < Danger < Unreadable`) e' usato dal confronto `l > level`; `Unreadable` non viene mai prodotto da `Evaluate`.

- [ ] **Step 3:** test verdi.

---

### Task 3: `DiskReport` (modello + JSON) e `SmartReader` (P/Invoke)

**Files:** Create `src/RoboKeep.Core/Services/Smart/DiskReport.cs`, `SmartReader.cs`, `src/RoboKeep.Tests/DiskReportTests.cs`, `SmartReaderTests.cs`.

- [ ] **Step 1: modello**

```csharp
using System.Text.Json;

namespace RoboKeep.Core.Services.Smart;

public enum DiskBus { Unknown, Nvme, Sata, Usb, Other }

/// <summary>Un disco fisico con il suo SMART (uno dei due, o nessuno se non leggibile).
/// <paramref name="AtaBlock"/> e' il blocco grezzo da 512 byte (Base64 nel JSON), cosi' l'helper
/// non deve conoscere le regole: decodifica e verdetto restano nell'app.</summary>
public sealed record DiskReport(
    int Number, string Model, string Serial, DiskBus Bus, string[] Letters,
    byte[]? AtaBlock, byte[]? NvmeLog, string? Error)
{
    public SmartAttributes? Ata => AtaBlock is null ? null : SmartAttributes.ParseAta(AtaBlock);
    public NvmeHealth? Nvme => NvmeLog is null ? null : SmartAttributes.ParseNvme(NvmeLog);
    public bool IsReadable => Ata is not null || Nvme is not null;

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static string ToJson(IEnumerable<DiskReport> reports) => JsonSerializer.Serialize(reports.ToList(), Options);
    public static List<DiskReport> FromJson(string json) => JsonSerializer.Deserialize<List<DiskReport>>(json, Options) ?? new();
}
```

Test `DiskReportTests`: round-trip di due dischi (uno con `AtaBlock` = blocco del test di Task 1, uno con `NvmeLog`), `Ata`/`Nvme` decodificati dopo il round-trip, `IsReadable` false con entrambi null.

- [ ] **Step 2: `SmartReader`** (i valori numerici vengono dalle sonde; non cambiarli)

```csharp
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace RoboKeep.Core.Services.Smart;

/// <summary>
/// Accesso ai dischi fisici. <see cref="ListPhysicalDisks"/> e <see cref="ReadNvme"/> non
/// richiedono privilegi; <see cref="ReadAta"/> (ATA PASS-THROUGH via SCSI pass-through, l'unica
/// via che attraversa i box USB) apre il disco in lettura+scrittura e richiede l'amministratore:
/// lo chiama solo il helper elevato. Tutto best-effort: null su errore, mai eccezioni.
/// </summary>
public static class SmartReader
{
    private const uint GenericRead = 0x80000000, GenericWrite = 0x40000000;
    private const uint ShareRW = 0x1 | 0x2, OpenExisting = 3;
    private const uint IoctlStorageQueryProperty = 0x002D1400;
    private const uint IoctlScsiPassThroughDirect = 0x0004D014;
    private const uint IoctlStorageGetDeviceNumber = 0x002D1080;
    public const int MaxDrives = 16;

    public static string DevicePath(int drive) => $@"\\.\PhysicalDrive{drive}";

    /// <summary>Dischi fisici presenti (0..MaxDrives-1 che si aprono), con modello, seriale, bus e
    /// lettere delle unita' che ospitano.</summary>
    public static List<DiskReport> ListPhysicalDisks()
    {
        var letters = LettersByDisk();
        var list = new List<DiskReport>();
        for (var n = 0; n < MaxDrives; n++)
        {
            using var h = CreateFileW(DevicePath(n), 0, ShareRW, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (h.IsInvalid) continue;
            var (model, serial, bus) = QueryDevice(h);
            list.Add(new DiskReport(n, model, serial, bus, letters.TryGetValue(n, out var l) ? l.ToArray() : Array.Empty<string>(), null, null, null));
        }
        return list;
    }

    /// <summary>Blocco SMART ATA (512 byte) via SCSI pass-through, o null. Richiede admin.</summary>
    public static byte[]? ReadAta(int drive)
    {
        try
        {
            using var h = CreateFileW(DevicePath(drive), GenericRead | GenericWrite, ShareRW, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (h.IsInvalid) return null;
            var data = Marshal.AllocHGlobal(512);
            try
            {
                // Buffer azzerato: su un disco che non risponde (NVMe) resterebbe spazzatura.
                for (var i = 0; i < 512; i += 8) Marshal.WriteInt64(data, i, 0);
                // SCSI_PASS_THROUGH_DIRECT (x64, 56 byte) + 32 byte di sense.
                var buf = new byte[56 + 32];
                BitConverter.GetBytes((ushort)56).CopyTo(buf, 0);
                buf[6] = 16;  // CdbLength
                buf[7] = 32;  // SenseInfoLength
                buf[8] = 1;   // SCSI_IOCTL_DATA_IN
                BitConverter.GetBytes((uint)512).CopyTo(buf, 12);  // DataTransferLength
                BitConverter.GetBytes((uint)10).CopyTo(buf, 16);   // TimeOutValue (s)
                BitConverter.GetBytes((long)data).CopyTo(buf, 24); // DataBuffer
                BitConverter.GetBytes((uint)56).CopyTo(buf, 32);   // SenseInfoOffset
                // ATA PASS-THROUGH(16): SMART READ DATA (B0h, features D0h, LBA 4Fh/C2h), PIO data-in, 1 blocco.
                new byte[] { 0x85, 0x08, 0x0E, 0, 0xD0, 0, 1, 0, 0, 0, 0x4F, 0, 0xC2, 0xA0, 0xB0, 0 }.CopyTo(buf, 36);
                if (!DeviceIoControl(h, IoctlScsiPassThroughDirect, buf, (uint)buf.Length, buf, (uint)buf.Length, out _, IntPtr.Zero)) return null;
                if (buf[2] != 0) return null; // ScsiStatus != GOOD: il dispositivo non ha risposto al comando ATA
                var block = new byte[512];
                Marshal.Copy(data, block, 0, 512);
                return SmartAttributes.ParseAta(block) is null ? null : block;
            }
            finally { Marshal.FreeHGlobal(data); }
        }
        catch { return null; }
    }

    /// <summary>Log SMART/Health NVMe (512 byte), o null. Non richiede admin.</summary>
    public static byte[]? ReadNvme(int drive)
    {
        try
        {
            using var h = CreateFileW(DevicePath(drive), 0, ShareRW, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (h.IsInvalid) return null;
            var buf = new byte[8 + 40 + 512];
            BitConverter.GetBytes(50).CopyTo(buf, 0);   // StorageDeviceProtocolSpecificProperty
            BitConverter.GetBytes(0).CopyTo(buf, 4);    // PropertyStandardQuery
            const int p = 8;                             // STORAGE_PROTOCOL_SPECIFIC_DATA
            BitConverter.GetBytes(3).CopyTo(buf, p);     // ProtocolTypeNvme
            BitConverter.GetBytes(2).CopyTo(buf, p + 4); // NVMeDataTypeLogPage
            BitConverter.GetBytes(2).CopyTo(buf, p + 8); // log page 2 = SMART/Health
            BitConverter.GetBytes(0).CopyTo(buf, p + 12);
            BitConverter.GetBytes(40).CopyTo(buf, p + 16);  // ProtocolDataOffset
            BitConverter.GetBytes(512).CopyTo(buf, p + 20); // ProtocolDataLength
            if (!DeviceIoControl(h, IoctlStorageQueryProperty, buf, (uint)buf.Length, buf, (uint)buf.Length, out _, IntPtr.Zero)) return null;
            var log = new byte[512];
            Array.Copy(buf, 8 + 40, log, 0, 512);
            return log;
        }
        catch { return null; }
    }

    // STORAGE_DEVICE_DESCRIPTOR via StorageDeviceProperty (0): offset 12 ProductIdOffset, 20 SerialNumberOffset, 28 BusType.
    private static (string Model, string Serial, DiskBus Bus) QueryDevice(SafeFileHandle h)
    {
        try
        {
            var query = new byte[12]; // PropertyId 0, QueryType 0, AdditionalParameters
            var buf = new byte[1024];
            if (!DeviceIoControl(h, IoctlStorageQueryProperty, query, 12, buf, (uint)buf.Length, out var ret, IntPtr.Zero) || ret < 36)
                return ("?", "", DiskBus.Unknown);
            string At(int offsetField)
            {
                var off = BitConverter.ToInt32(buf, offsetField);
                if (off <= 0 || off >= ret) return "";
                var end = Array.IndexOf(buf, (byte)0, off);
                return System.Text.Encoding.ASCII.GetString(buf, off, (end < 0 ? (int)ret : end) - off).Trim();
            }
            var bus = BitConverter.ToInt32(buf, 28) switch { 17 => DiskBus.Nvme, 11 => DiskBus.Sata, 7 => DiskBus.Usb, 3 => DiskBus.Sata /*ATA*/, _ => DiskBus.Other };
            var vendor = At(8);
            var product = At(12);
            var model = string.IsNullOrEmpty(vendor) ? product : $"{vendor} {product}".Trim();
            return (string.IsNullOrEmpty(model) ? "?" : model, At(20), bus);
        }
        catch { return ("?", "", DiskBus.Unknown); }
    }

    // Lettera -> numero disco, via IOCTL_STORAGE_GET_DEVICE_NUMBER sul volume.
    private static Dictionary<int, List<string>> LettersByDisk()
    {
        var map = new Dictionary<int, List<string>>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (d.DriveType != DriveType.Fixed && d.DriveType != DriveType.Removable) continue;
                using var h = CreateFileW($@"\\.\{d.Name[0]}:", 0, ShareRW, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
                if (h.IsInvalid) continue;
                var outBuf = new byte[12];
                if (!DeviceIoControl(h, IoctlStorageGetDeviceNumber, null, 0, outBuf, 12, out _, IntPtr.Zero)) continue;
                var n = BitConverter.ToInt32(outBuf, 4);
                if (!map.TryGetValue(n, out var list)) map[n] = list = new List<string>();
                list.Add(d.Name.TrimEnd('\\'));
            }
            catch { /* unita' non pronta: si salta */ }
        }
        return map;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, byte[]? lpInBuffer, uint nInBufferSize,
        byte[] lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);
}
```

Attenzione al `QueryDevice`: nella sonda il bus NVMe e' risultato 17 e USB 7 (STORAGE_BUS_TYPE). `VendorIdOffset` e' all'offset 8, `ProductIdOffset` 12, `SerialNumberOffset` 20, `BusType` 28 (STORAGE_DEVICE_DESCRIPTOR). Se `ListPhysicalDisks` non riporta "SAMSUNG HD103SI" e "ORICO" sulla macchina dell'utente, correggere gli offset leggendo la documentazione di STORAGE_DEVICE_DESCRIPTOR, non a tentativi.

- [ ] **Step 3: test `SmartReaderTests`** (sulla macchina reale, senza admin)

```csharp
using RoboKeep.Core.Services.Smart;

namespace RoboKeep.Tests;

public class SmartReaderTests
{
    [Fact]
    public void ListPhysicalDisks_FindsAtLeastOneDisk_WithModelAndLetters()
    {
        var disks = SmartReader.ListPhysicalDisks();
        Assert.NotEmpty(disks);
        Assert.All(disks, d => Assert.False(string.IsNullOrWhiteSpace(d.Model)));
        // Il disco di sistema ospita C:
        Assert.Contains(disks, d => d.Letters.Contains("C:"));
    }

    [Fact]
    public void ReadNvme_OnAnNvmeDisk_ParsesWithoutAdmin_OrReturnsNullElsewhere()
    {
        foreach (var d in SmartReader.ListPhysicalDisks())
        {
            var log = SmartReader.ReadNvme(d.Number);
            if (d.Bus == DiskBus.Nvme) { Assert.NotNull(log); Assert.NotNull(SmartAttributes.ParseNvme(log!)); }
        }
    }

    [Fact]
    public void ReadAta_WithoutAdmin_NeverThrows()
    {
        foreach (var d in SmartReader.ListPhysicalDisks())
            _ = SmartReader.ReadAta(d.Number); // senza admin torna null; non deve lanciare
    }
}
```

- [ ] **Step 4:** suite verde; verificare a mano che `ListPhysicalDisks` sulla macchina dell'utente riporti i due dischi con modello e lettere corretti (scrivere un test temporaneo che stampa con `ITestOutputHelper`, oppure un piccolo `dotnet run` nello scratchpad).

---

### Task 4: helper elevato e sessione

**Files:** Create `src/RoboKeep.Core/Services/Smart/SmartHelper.cs`, `SmartSession.cs`; Modify `src/RoboKeep/CliOptions.cs`, `src/RoboKeep/App.xaml.cs`.

- [ ] **Step 1: `SmartHelper`**

```csharp
namespace RoboKeep.Core.Services.Smart;

/// <summary>Corpo della modalita' <c>--smart-helper &lt;dir&gt;</c> (processo elevato): elenca i
/// dischi, legge lo SMART ATA di quelli non NVMe, scrive smart.json e termina. Nessuna UI.</summary>
public static class SmartHelper
{
    public const string ResultFile = "smart.json";

    public static int Run(string dir)
    {
        try
        {
            var reports = SmartReader.ListPhysicalDisks().Select(d =>
                d.Bus == DiskBus.Nvme ? d : d with { AtaBlock = SmartReader.ReadAta(d.Number),
                    Error = SmartReader.ReadAta(d.Number) is null ? "ata" : null }).ToList();
            // NB: una sola lettura per disco: riscrivere senza la doppia chiamata a ReadAta.
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, ResultFile), DiskReport.ToJson(reports));
            return 0;
        }
        catch { return 1; }
    }
}
```

(Scrivere la versione corretta: `var block = SmartReader.ReadAta(d.Number); d with { AtaBlock = block, Error = block is null ? "ata" : null }`.)

- [ ] **Step 2: `SmartSession`**

```csharp
using System.ComponentModel;
using System.Diagnostics;

namespace RoboKeep.Core.Services.Smart;

/// <summary>Esito complessivo della lettura: i dischi e se l'elevazione e' stata negata/fallita.</summary>
public sealed record SmartSnapshot(IReadOnlyList<DiskReport> Disks, bool ElevationDenied, bool HelperFailed);

/// <summary>
/// Lato app: legge subito cio' che non richiede privilegi (elenco dischi, NVMe), poi — se ci sono
/// dischi non NVMe — avvia il helper elevato (prompt UAC, come per VSS), ne legge smart.json e
/// fonde i risultati. Cartella di sessione usa-e-getta sotto <paramref name="sessionRoot"/>.
/// </summary>
public static class SmartSession
{
    private const int ErrorCancelled = 1223;
    private static readonly TimeSpan HelperTimeout = TimeSpan.FromSeconds(60);

    public static async Task<SmartSnapshot> ReadAsync(string sessionRoot, CancellationToken ct = default)
    {
        var disks = await Task.Run(() => SmartReader.ListPhysicalDisks()
            .Select(d => d.Bus == DiskBus.Nvme ? d with { NvmeLog = SmartReader.ReadNvme(d.Number) } : d).ToList(), ct)
            .ConfigureAwait(false);
        if (!disks.Any(d => d.Bus != DiskBus.Nvme)) return new SmartSnapshot(disks, false, false);

        var exe = Environment.ProcessPath;
        if (exe is null) return new SmartSnapshot(disks, false, true);
        var dir = Path.Combine(sessionRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Process helper;
            try
            {
                helper = await Task.Run(() => Process.Start(new ProcessStartInfo
                {
                    FileName = exe, Arguments = $"--smart-helper \"{dir}\"", UseShellExecute = true, Verb = "runas",
                }), ct).ConfigureAwait(false) ?? throw new Win32Exception("avvio fallito");
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled) { return new SmartSnapshot(disks, true, false); }
            catch (Win32Exception) { return new SmartSnapshot(disks, false, true); }

            using (helper)
            {
                var exited = await Task.Run(() => helper.WaitForExit((int)HelperTimeout.TotalMilliseconds), ct).ConfigureAwait(false);
                var file = Path.Combine(dir, SmartHelper.ResultFile);
                if (!exited || !File.Exists(file)) return new SmartSnapshot(disks, false, true);
                var fromHelper = DiskReport.FromJson(File.ReadAllText(file));
                // Fusione: per i dischi non NVMe si prende il blocco ATA letto dal helper.
                var merged = disks.Select(d => fromHelper.FirstOrDefault(h => h.Number == d.Number) is { } x && d.Bus != DiskBus.Nvme
                    ? d with { AtaBlock = x.AtaBlock, Error = x.Error } : d).ToList();
                return new SmartSnapshot(merged, false, false);
            }
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
```

- [ ] **Step 3: `CliOptions`** — aggiungere `public string? SmartHelperDir { get; private set; }` e il caso `"--smart-helper"` (come `--vss-helper`). `App.xaml.cs`, prima del blocco VSS:

```csharp
        if (options.SmartHelperDir is not null)
        {
            // Helper elevato per lo SMART: legge e scrive smart.json, poi esce. Niente GUI.
            Shutdown(RoboKeep.Core.Services.Smart.SmartHelper.Run(options.SmartHelperDir));
            return;
        }
```

- [ ] **Step 4:** build 0 avvisi; prova manuale da riga di comando: `RoboKeep.exe --smart-helper <cartella>` eseguito da un prompt **amministratore** deve produrre `smart.json` con `AtaBlock` valorizzato per il disco USB; senza admin `AtaBlock` null ed `Error: "ata"`.

---

### Task 5: testi (5 lingue)

**Files:** Modify `src/RoboKeep/Localization/Loc.cs` (accanto a `Main_History` per `Main_DiskHealth`; le altre in un blocco nuovo `// --- Salute dischi ---` in ogni sezione).

| chiave | it | en | es | fr | de |
|---|---|---|---|---|---|
| Main_DiskHealth | Salute dischi | Disk health | Salud de discos | État des disques | Datenträgerzustand |
| Smart_Title | Salute dei dischi | Disk health | Salud de los discos | État des disques | Zustand der Datenträger |
| Smart_Intro | Lettura SMART di ogni disco collegato. Per i dischi SATA e USB serve l'autorizzazione di amministratore (una richiesta per ogni lettura). Solo lettura: nessun test avviato, nulla scritto sui dischi. | SMART reading of every connected disk. SATA and USB disks need administrator approval (one prompt per reading). Read-only: no test is started, nothing is written to the disks. | Lectura SMART de cada disco conectado. Los discos SATA y USB necesitan autorización de administrador (una petición por lectura). Solo lectura: no se inicia ningún test ni se escribe nada en los discos. | Lecture SMART de chaque disque connecté. Les disques SATA et USB nécessitent l'autorisation administrateur (une demande par lecture). Lecture seule : aucun test lancé, rien n'est écrit sur les disques. | SMART-Auslesung jedes angeschlossenen Datenträgers. SATA- und USB-Datenträger benötigen die Administratorfreigabe (eine Abfrage pro Auslesung). Nur lesend: kein Test wird gestartet, nichts wird geschrieben. |
| Smart_Refresh | Aggiorna | Refresh | Actualizar | Actualiser | Aktualisieren |
| Smart_Reading | Lettura in corso… | Reading… | Leyendo… | Lecture… | Wird gelesen… |
| Smart_Good | Buono | Good | Bueno | Bon | Gut |
| Smart_Warning | Attenzione | Caution | Atención | Attention | Achtung |
| Smart_Danger | Pericolo | Danger | Peligro | Danger | Gefahr |
| Smart_Unreadable | Non leggibile | Not readable | No legible | Illisible | Nicht lesbar |
| Smart_NeedsAdmin | Serve l'autorizzazione di amministratore per leggere questo disco: premi Aggiorna e accetta la richiesta. | Administrator approval is needed to read this disk: click Refresh and accept the prompt. | Se necesita autorización de administrador para leer este disco: pulsa Actualizar y acepta la petición. | L'autorisation administrateur est nécessaire pour lire ce disque : cliquez sur Actualiser et acceptez la demande. | Zum Auslesen dieses Datenträgers ist die Administratorfreigabe nötig: auf Aktualisieren klicken und die Abfrage bestätigen. |
| Smart_BridgeNoSmart | Il box USB non permette di leggere lo SMART di questo disco. | This USB enclosure does not allow reading the disk's SMART data. | La caja USB no permite leer el SMART de este disco. | Le boîtier USB ne permet pas de lire le SMART de ce disque. | Das USB-Gehäuse erlaubt das Auslesen der SMART-Daten dieses Datenträgers nicht. |
| Smart_Events | Errori nel registro eventi di Windows (ultimi 14 giorni): {0} | Errors in the Windows event log (last 14 days): {0} | Errores en el registro de eventos de Windows (últimos 14 días): {0} | Erreurs dans le journal des événements Windows (14 derniers jours) : {0} | Fehler im Windows-Ereignisprotokoll (letzte 14 Tage): {0} |
| Smart_Reallocated | Settori riallocati (05) | Reallocated sectors (05) | Sectores reasignados (05) | Secteurs réalloués (05) | Neu zugewiesene Sektoren (05) |
| Smart_Reallocated_Hint | Settori danneggiati sostituiti dalla riserva. Sopra zero il disco sta cedendo: sostituiscilo. | Bad sectors replaced from the spare pool. Above zero the disk is failing: replace it. | Sectores dañados sustituidos por la reserva. Por encima de cero el disco está fallando: sustitúyelo. | Secteurs défectueux remplacés par la réserve. Au-dessus de zéro, le disque lâche : remplacez-le. | Defekte Sektoren, durch Reserve ersetzt. Über null fällt der Datenträger aus: ersetzen. |
| Smart_Pending | Settori in attesa (C5) | Pending sectors (C5) | Sectores pendientes (C5) | Secteurs en attente (C5) | Ausstehende Sektoren (C5) |
| Smart_Pending_Hint | Settori illeggibili non ancora riscritti: spesso scritture interrotte (cavo, alimentazione). Una formattazione completa li riscrive; se poi i riallocati (05) salgono, il disco sta cedendo. | Unreadable sectors not yet rewritten: often interrupted writes (cable, power). A full format rewrites them; if reallocated (05) then rises, the disk is failing. | Sectores ilegibles aún no reescritos: a menudo escrituras interrumpidas (cable, alimentación). Un formateo completo los reescribe; si después suben los reasignados (05), el disco está fallando. | Secteurs illisibles pas encore réécrits : souvent des écritures interrompues (câble, alimentation). Un formatage complet les réécrit ; si les réalloués (05) augmentent ensuite, le disque lâche. | Unlesbare, noch nicht neu geschriebene Sektoren: oft unterbrochene Schreibvorgänge (Kabel, Strom). Eine vollständige Formatierung schreibt sie neu; steigen danach die neu zugewiesenen (05), fällt der Datenträger aus. |
| Smart_Uncorrectable | Settori non correggibili (C6) | Uncorrectable sectors (C6) | Sectores no corregibles (C6) | Secteurs non corrigeables (C6) | Nicht korrigierbare Sektoren (C6) |
| Smart_Uncorrectable_Hint | Dati persi per sempre in quei settori. Sopra zero: sostituisci il disco. | Data lost for good in those sectors. Above zero: replace the disk. | Datos perdidos definitivamente en esos sectores. Por encima de cero: sustituye el disco. | Données perdues définitivement dans ces secteurs. Au-dessus de zéro : remplacez le disque. | Daten in diesen Sektoren endgültig verloren. Über null: Datenträger ersetzen. |
| Smart_LinkErrors | Errori CRC sul collegamento (C7) | CRC errors on the link (C7) | Errores CRC en el enlace (C7) | Erreurs CRC sur la liaison (C7) | CRC-Fehler auf der Verbindung (C7) |
| Smart_LinkErrors_Hint | Errori di trasmissione tra box (o controller) e disco, non sul disco. Se cresce: cavo, box o alimentazione. Un cavo USB difettoso non lascia traccia qui. | Transmission errors between the enclosure (or controller) and the disk, not on the disk. If it grows: cable, enclosure or power. A faulty USB cable leaves no trace here. | Errores de transmisión entre la caja (o controlador) y el disco, no en el disco. Si crece: cable, caja o alimentación. Un cable USB defectuoso no deja rastro aquí. | Erreurs de transmission entre le boîtier (ou le contrôleur) et le disque, pas sur le disque. S'il augmente : câble, boîtier ou alimentation. Un câble USB défectueux ne laisse aucune trace ici. | Übertragungsfehler zwischen Gehäuse (oder Controller) und Datenträger, nicht auf dem Datenträger. Steigt der Wert: Kabel, Gehäuse oder Strom. Ein defektes USB-Kabel hinterlässt hier keine Spur. |
| Smart_ReportedUncorrectable | Errori non correggibili segnalati (BB) | Reported uncorrectable errors (BB) | Errores no corregibles notificados (BB) | Erreurs non corrigeables signalées (BB) | Gemeldete nicht korrigierbare Fehler (BB) |
| Smart_ReportedUncorrectable_Hint | Storico delle letture fallite. Non si azzera: conta se cresce tra un controllo e l'altro. | History of failed reads. It never resets: what matters is whether it grows between checks. | Histórico de lecturas fallidas. No se pone a cero: importa si crece entre un control y otro. | Historique des lectures échouées. Ne se remet jamais à zéro : ce qui compte, c'est s'il augmente entre deux contrôles. | Verlauf fehlgeschlagener Lesevorgänge. Wird nie zurückgesetzt: entscheidend ist, ob er zwischen zwei Prüfungen wächst. |
| Smart_Temperature | Temperatura | Temperature | Temperatura | Température | Temperatur |
| Smart_Temperature_Hint | Sopra 50 °C un disco meccanico soffre; un NVMe è normale fino a 70 °C. | Above 50 °C a mechanical disk suffers; an NVMe is fine up to 70 °C. | Por encima de 50 °C un disco mecánico sufre; un NVMe es normal hasta 70 °C. | Au-dessus de 50 °C un disque mécanique souffre ; un NVMe est normal jusqu'à 70 °C. | Über 50 °C leidet eine mechanische Festplatte; eine NVMe ist bis 70 °C normal. |
| Smart_PowerOnHours | Ore di accensione | Power-on hours | Horas de encendido | Heures de fonctionnement | Betriebsstunden |
| Smart_PowerOnHours_Hint | Età di lavoro del disco. | The disk's working age. | Edad de trabajo del disco. | Âge de fonctionnement du disque. | Betriebsalter des Datenträgers. |
| Smart_NvmeCritical | Avviso critico | Critical warning | Aviso crítico | Avertissement critique | Kritische Warnung |
| Smart_NvmeCritical_Hint | Diverso da 0 = il disco stesso segnala un problema grave. | Non-zero = the disk itself reports a serious problem. | Distinto de 0 = el propio disco notifica un problema grave. | Différent de 0 = le disque lui-même signale un problème grave. | Ungleich 0 = der Datenträger selbst meldet ein ernstes Problem. |
| Smart_NvmeSpare | Riserva disponibile | Available spare | Reserva disponible | Réserve disponible | Verfügbare Reserve |
| Smart_NvmeSpare_Hint | Celle di ricambio. Sotto la soglia: sostituisci il disco. | Spare cells. Below the threshold: replace the disk. | Celdas de repuesto. Por debajo del umbral: sustituye el disco. | Cellules de rechange. Sous le seuil : remplacez le disque. | Ersatzzellen. Unter der Schwelle: Datenträger ersetzen. |
| Smart_NvmeMediaErrors | Errori del supporto | Media errors | Errores del soporte | Erreurs du support | Medienfehler |
| Smart_NvmeMediaErrors_Hint | Dati non recuperabili. Sopra zero: sostituisci il disco. | Unrecoverable data. Above zero: replace the disk. | Datos no recuperables. Por encima de cero: sustituye el disco. | Données irrécupérables. Au-dessus de zéro : remplacez le disque. | Nicht wiederherstellbare Daten. Über null: Datenträger ersetzen. |
| Smart_NvmeUsed | Usura | Wear | Desgaste | Usure | Abnutzung |
| Smart_NvmeUsed_Hint | Percentuale di vita consumata secondo il produttore. | Percentage of life used according to the maker. | Porcentaje de vida consumida según el fabricante. | Pourcentage de vie consommée selon le fabricant. | Verbrauchter Lebensdaueranteil laut Hersteller. |
| Smart_NvmeUnsafeShutdowns | Spegnimenti non protetti | Unsafe shutdowns | Apagados no protegidos | Arrêts non protégés | Ungesicherte Abschaltungen |
| Smart_NvmeUnsafeShutdowns_Hint | Corrente tolta senza avviso. Non è un guasto, ma non deve crescere di continuo. | Power cut without warning. Not a fault, but it should not keep growing. | Corriente cortada sin aviso. No es un fallo, pero no debe crecer sin parar. | Courant coupé sans avertissement. Pas une panne, mais cela ne doit pas augmenter sans cesse. | Strom ohne Vorwarnung getrennt. Kein Defekt, sollte aber nicht ständig steigen. |
| Smart_Bus_Nvme / Smart_Bus_Sata / Smart_Bus_Usb / Smart_Bus_Other | NVMe / SATA / USB / Altro | NVMe / SATA / USB / Other | NVMe / SATA / USB / Otro | NVMe / SATA / USB / Autre | NVMe / SATA / USB / Sonstige |

- [ ] `LocParityTests` verde.

---

### Task 6: finestra e pulsante

**Files:** Create `src/RoboKeep/DiskHealthWindow.xaml`, `DiskHealthWindow.xaml.cs`, `ViewModels/DiskHealthViewModel.cs`; Modify `src/RoboKeep/MainWindow.xaml` (pulsante dopo Cronologia), `MainWindow.xaml.cs` (apertura, una sola istanza come la guida), `AppHost.cs` (`SmartSessionRoot => Path.Combine(Store.DirectoryPath, "smart")`).

- [ ] **Step 1: view model**

```csharp
using System.Collections.ObjectModel;
using RoboKeep.Core.Services;
using RoboKeep.Core.Services.Smart;
using RoboKeep.Infra;
using RoboKeep.Localization;

namespace RoboKeep.ViewModels;

/// <summary>Una riga del referto, gia' tradotta.</summary>
public sealed record DiskFindingRow(string Label, string Hint, string Value, DiskHealthLevel Level);

/// <summary>Una scheda disco: intestazione, verdetto, righe, registro eventi.</summary>
public sealed class DiskCardViewModel
{
    public string Title { get; init; } = "";        // "SAMSUNG HD103SI — USB — E:"
    public DiskHealthLevel Level { get; init; }
    public string LevelText { get; init; } = "";
    public string? Note { get; init; }               // Smart_NeedsAdmin / Smart_BridgeNoSmart
    public string EventsText { get; init; } = "";
    public IReadOnlyList<DiskFindingRow> Rows { get; init; } = Array.Empty<DiskFindingRow>();
}

public sealed class DiskHealthViewModel : ObservableObject
{
    private readonly AppHost _host;
    private bool _busy;
    public ObservableCollection<DiskCardViewModel> Disks { get; } = new();
    public bool IsBusy { get => _busy; private set => SetField(ref _busy, value); }
    private string _status = "";
    public string Status { get => _status; private set => SetField(ref _status, value); }

    public DiskHealthViewModel(AppHost host) => _host = host;

    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true; Status = Loc.Instance["Smart_Reading"];
        try
        {
            var snap = await SmartSession.ReadAsync(_host.SmartSessionRoot);
            Disks.Clear();
            foreach (var d in snap.Disks) Disks.Add(Build(d, snap.ElevationDenied));
            Status = "";
        }
        catch (Exception ex) { Status = ex.Message; }
        finally { IsBusy = false; }
    }

    private static DiskCardViewModel Build(DiskReport d, bool elevationDenied)
    {
        var bus = Loc.Instance[d.Bus switch { DiskBus.Nvme => "Smart_Bus_Nvme", DiskBus.Sata => "Smart_Bus_Sata", DiskBus.Usb => "Smart_Bus_Usb", _ => "Smart_Bus_Other" }];
        var title = $"{d.Model} — {bus}" + (d.Letters.Length > 0 ? " — " + string.Join(", ", d.Letters) : "");
        var events = d.Letters.Select(l => DiskEventLog.Collect(l + @"\")).Aggregate(DiskEventSummary.None,
            (a, b) => new DiskEventSummary(a.BadBlocks + b.BadBlocks, a.IoErrors + b.IoErrors, a.FileSystemErrors + b.FileSystemErrors, b.Latest ?? a.Latest));
        var eventsText = string.Format(Loc.Instance["Smart_Events"], events.Total == 0 ? "0" : events.Describe());

        DiskVerdictResult? v = d.Nvme is { } n ? DiskVerdict.Evaluate(n) : d.Ata is { } a ? DiskVerdict.Evaluate(a) : null;
        if (v is null)
        {
            return new DiskCardViewModel
            {
                Title = title, Level = DiskHealthLevel.Unreadable, LevelText = Loc.Instance["Smart_Unreadable"],
                Note = Loc.Instance[elevationDenied || d.Error is null ? "Smart_NeedsAdmin" : "Smart_BridgeNoSmart"], EventsText = eventsText,
            };
        }
        return new DiskCardViewModel
        {
            Title = title, Level = v.Level, LevelText = Loc.Instance[v.Level switch { DiskHealthLevel.Danger => "Smart_Danger", DiskHealthLevel.Warning => "Smart_Warning", _ => "Smart_Good" }],
            EventsText = eventsText,
            Rows = v.Findings.Select(f => new DiskFindingRow(Loc.Instance[f.Key], Loc.Instance[f.Key + "_Hint"], f.Value, f.Level)).ToList(),
        };
    }
}
```

Nota: `d.Error is null` con `Ata` null e disco non NVMe = il helper non ha girato (UAC negato o fallito) → "serve l'autorizzazione"; `Error == "ata"` = il helper ha provato e il bridge non risponde → "il box non permette".

- [ ] **Step 2: XAML** (stessa struttura di `SnapshotsWindow.xaml`: `ui:FluentWindow`, `ui:TitleBar` con `SymbolIcon HardDrive24` — verificare il simbolo — testo `Smart_Intro`, pulsante `Smart_Refresh` (disabilitato con `IsBusy`), `TextBlock` `Status`, e un `ItemsControl` di `Disks` dentro uno `ScrollViewer`). Ogni scheda: `ui:Card` con intestazione (Title in grassetto + LevelText colorato: Good `#1B8A3E`, Warning `#C8860D`, Danger `#B00020`, Unreadable grigio; usare un `DataTrigger` su `Level`), la `Note` se presente, una griglia a tre colonne (Label / Value / Hint, con Hint in stile `Hint` opacita' 0.7 e a capo) per `Rows`, e in fondo `EventsText`. La finestra e' non modale (`Show()`), una sola istanza, 760×640, `CenterOwner`.

- [ ] **Step 3: code-behind e apertura** — `DiskHealthWindow(AppHost host)`: crea il view model, `Loaded += async (_, _) => await _vm.RefreshAsync();`, pulsante Aggiorna → `RefreshAsync`. `MainWindow`: pulsante `<ui:Button Icon="{ui:SymbolIcon HardDrive24}" Content="{l:Tr Main_DiskHealth}" Click="OnDiskHealth" Margin="0,0,8,0"/>` dopo Cronologia; `OnDiskHealth` con lo stesso schema a istanza singola di `OpenGuide` (`_diskHealth`).

- [ ] **Step 4:** build 0 avvisi, suite verde, `LocParity` verde. Prova manuale con il Samsung collegato: dopo l'UAC i valori devono coincidere con CrystalDiskInfo (05=0, C5=0, C7=9, BB=1138, ore ≈ 387+); l'NVMe mostra temperatura, riserva, usura, spegnimenti non protetti; rifiutando l'UAC il Samsung risulta "Non leggibile — serve l'autorizzazione" e l'NVMe resta leggibile.

---

### Task 7: documentazione

- [ ] Cap. 16 (it/en): sezione «Leggere la salute del disco» — cosa significano Buono/Attenzione/Pericolo, 05 vs C5 (con l'esempio: C5 alto e 05 a zero = scritture interrotte, formattazione completa e ricontrollo), C7 = collegamento, BB = storico. Cap. 14: il pulsante e il prompt UAC. Cap. 17: la lettura e' locale, nulla esce dal PC. CHANGELOG (Unreleased → Added): «**Disk health window.** One click reads every disk's SMART (NVMe without prompts; SATA/USB through a one-time administrator prompt, the only way through USB enclosures) and gives a plain-language verdict — Good, Caution, Danger — with the handful of values that matter (reallocated, pending and uncorrectable sectors, link CRC errors, temperature, hours; spare, wear and media errors for NVMe), each explained. Read-only; nothing is written or tested.»

---

### Task 8: verifica finale

- [ ] Suite Debug e Release verdi; build 0 avvisi; unix2dos; prova manuale del Task 6 confermata dall'utente; commit `feat: finestra Salute dischi - SMART via helper elevato con verdetto spiegato` solo dopo conferma.

---

## Auto-verifica

- Spec coperta: parse (T1), verdetto (T2), lettura NVMe senza admin e ATA via helper (T3-T4), UAC negato → non leggibile (T4, T6), registro eventi per disco (T6), testi e guida (T5, T7), solo lettura (nessuna scrittura in T3-T4).
- Nomi coerenti: `SmartAttributes.{ParseAta,ParseNvme,TemperatureC,PowerOnHours,TryGet}`, `NvmeHealth`, `DiskVerdict.Evaluate` (2 overload) → `DiskVerdictResult(Level, Findings)`, `DiskFinding(Key, Value, Level)`, `DiskHealthLevel`, `DiskReport(Number, Model, Serial, Bus, Letters, AtaBlock, NvmeLog, Error)`, `DiskBus`, `SmartReader.{ListPhysicalDisks,ReadAta,ReadNvme}`, `SmartHelper.{Run,ResultFile}`, `SmartSession.ReadAsync` → `SmartSnapshot`, `AppHost.SmartSessionRoot`, chiavi `Smart_*`, `Main_DiskHealth`.
