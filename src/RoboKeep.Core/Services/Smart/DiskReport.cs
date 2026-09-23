using System.Text.Json;
using System.Text.Json.Serialization;

namespace RoboKeep.Core.Services.Smart;

public enum DiskBus { Unknown, Nvme, Sata, Usb, Other }

/// <summary>Un disco fisico con il suo SMART (uno dei due, o nessuno se non leggibile).
/// <paramref name="AtaBlock"/> e' il blocco grezzo da 512 byte (Base64 nel JSON), cosi' l'helper
/// non deve conoscere le regole: decodifica e verdetto restano nell'app.</summary>
public sealed record DiskReport(
    int Number, string Model, string Serial, DiskBus Bus, string[] Letters,
    byte[]? AtaBlock, byte[]? NvmeLog, string? Error)
{
    // Viste calcolate: fuori dal JSON di scambio, che porta solo i buffer grezzi (altrimenti
    // smart.json duplicherebbe gli attributi gia' decodificati, triplicando il file).
    [JsonIgnore] public SmartAttributes? Ata => AtaBlock is null ? null : SmartAttributes.ParseAta(AtaBlock);
    [JsonIgnore] public NvmeHealth? Nvme => NvmeLog is null ? null : SmartAttributes.ParseNvme(NvmeLog);
    [JsonIgnore] public bool IsReadable => Ata is not null || Nvme is not null;

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static string ToJson(IEnumerable<DiskReport> reports) => JsonSerializer.Serialize(reports.ToList(), Options);
    public static List<DiskReport> FromJson(string json) => JsonSerializer.Deserialize<List<DiskReport>>(json, Options) ?? new();
}
