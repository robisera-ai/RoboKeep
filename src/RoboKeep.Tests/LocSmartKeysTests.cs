using RoboKeep.Core.Services.Smart;
using RoboKeep.Localization;
using static RoboKeep.Tests.SmartAttributesTests;

namespace RoboKeep.Tests;

/// <summary>Ogni riga del referto chiede a Loc due chiavi, l'etichetta e la spiegazione
/// (<c>Key</c> e <c>Key_Hint</c>): se una manca, la finestra Salute dischi lancia mentre si apre.
/// Qui si generano tutte le righe possibili — ATA e NVMe, con ogni valore diverso da zero perche'
/// nessuna riga resti fuori — e si controlla che le chiavi ci siano. La parita' tra le lingue la
/// garantisce <see cref="LocParityTests"/>: basta verificarle in italiano.</summary>
public class LocSmartKeysTests
{
    private static List<string> AllFindingKeys()
    {
        var ata = DiskVerdict.Evaluate(SmartAttributes.ParseAta(AtaBlock(
            (0x05, 90, 90, 4), (0xC5, 95, 95, 17), (0xC6, 90, 90, 2), (0xC7, 100, 100, 9),
            (0xBB, 100, 100, 1138), (0xC2, 71, 62, 41), (0x09, 95, 95, 387)))!);
        var nvme = DiskVerdict.Evaluate(new NvmeHealth(0x01, 64, 5, 10, 95, 738, 4, 3));
        return ata.Findings.Concat(nvme.Findings).Select(f => f.Key).Distinct().ToList();
    }

    [Fact]
    public void EveryFinding_HasLabelAndHint()
    {
        var it = Loc.Langs["it"];
        var keys = AllFindingKeys();
        // 7 righe ATA + 5 righe NVMe che non condividono la chiave (temperatura e ore sono comuni).
        // Se il verdetto aggiunge una riga il conto cambia e il test chiede la traduzione.
        Assert.Equal(12, keys.Count);
        foreach (var key in keys)
        {
            Assert.True(it.ContainsKey(key), $"Manca l'etichetta {key}");
            Assert.True(it.ContainsKey(key + "_Hint"), $"Manca la spiegazione {key}_Hint");
        }
    }

    [Fact]
    public void EveryBus_HasAName()
    {
        var it = Loc.Langs["it"];
        foreach (var key in new[] { "Smart_Bus_Nvme", "Smart_Bus_Sata", "Smart_Bus_Usb", "Smart_Bus_Other" })
            Assert.True(it.ContainsKey(key), $"Manca {key}");
    }
}
