using RoboKeep.Core.Services.Smart;

namespace RoboKeep.Tests;

/// <summary>Prove che toccano i dischi veri della macchina: l'esito dipende dall'hardware
/// presente, non solo dal codice. Sono marcate <c>Category=Hardware</c> per poterle escludere
/// (<c>dotnet test --filter Category!=Hardware</c>) senza toccare le altre.</summary>
public class SmartReaderTests
{
    [Fact]
    [Trait("Category", "Hardware")]
    public void ListPhysicalDisks_FindsAtLeastOneDisk_WithAModel()
    {
        var disks = SmartReader.ListPhysicalDisks();
        Assert.NotEmpty(disks);
        // Le lettere dipendono dalla macchina: su una macchina virtuale di CI il disco di sistema
        // puo' non essere il numero 0 e le unita' possono non essere montate. Il modello, invece,
        // lo dichiara ogni disco che si apre.
        Assert.All(disks, d => Assert.False(string.IsNullOrWhiteSpace(d.Model)));
    }

    [Fact]
    [Trait("Category", "Hardware")]
    public void ReadNvme_WhenTheDriverAnswers_ParsesWithoutAdmin()
    {
        foreach (var d in SmartReader.ListPhysicalDisks())
        {
            var log = SmartReader.ReadNvme(d.Number);
            // Non tutti i driver rispondono alla query del log (macchine virtuali, driver di terze
            // parti): null e' un esito ammesso. Un log che c'e' ma non si decodifica, no.
            if (log is not null) Assert.NotNull(SmartAttributes.ParseNvme(log));
        }
    }

    [Fact]
    [Trait("Category", "Hardware")]
    public void ReadAta_WithoutAdmin_NeverThrows()
    {
        foreach (var d in SmartReader.ListPhysicalDisks())
            _ = SmartReader.ReadAta(d.Number); // senza admin torna null; non deve lanciare
    }
}
