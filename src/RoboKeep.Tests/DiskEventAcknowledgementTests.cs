using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

/// <summary>
/// «Ho controllato»: gli errori del registro di Windows gia' guardati dall'utente non devono
/// ricomparire come avviso a ogni backup per due settimane. Si ricorda, per disco, fino a quale
/// errore e' stato controllato; quelli piu' recenti avvisano di nuovo.
/// </summary>
public class DiskEventAcknowledgementTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "RbcAck_" + Guid.NewGuid().ToString("N"));
    private string StorePath => Path.Combine(_dir, "disk-acks.json");

    private const string Samsung = @"\\?\Volume{5cae31e1-0000-0000-007e-000000000000}\";
    private static readonly DateTime Night = new(2026, 10, 1, 2, 7, 49);

    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    [Fact]
    public void AnAcknowledgement_IsRemembered_AfterReloading()
    {
        new DiskEventAcknowledgements(StorePath).Acknowledge(Samsung, Night);

        Assert.Equal(Night, new DiskEventAcknowledgements(StorePath).Until(Samsung));
        Assert.Null(new DiskEventAcknowledgements(StorePath).Until(@"\\?\Volume{altro}\"));
        Assert.Null(new DiskEventAcknowledgements(StorePath).Until(null));
    }

    [Fact]
    public void AnOlderAcknowledgement_DoesNotMoveTheDateBack()
    {
        var acks = new DiskEventAcknowledgements(StorePath);
        acks.Acknowledge(Samsung, Night);
        acks.Acknowledge(Samsung, Night.AddDays(-3));

        Assert.Equal(Night, acks.Until(Samsung));
    }

    [Fact]
    public void ErrorsAlreadyChecked_AreNotCounted_ButAreRemembered()
    {
        // I 295 errori della notte del 01/10, gia' controllati; poi un errore nuovo il giorno dopo.
        var events = new[]
        {
            ("disk", 51, Night.AddSeconds(-2)),
            ("Ntfs", 50, Night),
            ("disk", 51, Night.AddDays(1)),
        };

        var s = DiskEventLog.Summarize(events, acknowledgedUntil: Night);

        Assert.Equal(1, s.Total);           // solo quello nuovo avvisa
        Assert.Equal(1, s.IoErrors);
        Assert.Equal(2, s.Acknowledged);    // gli altri restano contati come «gia' controllati»
        Assert.Equal(Night, s.AcknowledgedUntil);
    }
}
