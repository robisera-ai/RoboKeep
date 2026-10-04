namespace RoboKeep.Tests;

/// <summary>
/// Una notifica di Windows rifiutata (icona del tray persa, notifiche disattivate) non deve
/// diventare un errore del backup appena riuscito, ne' far cadere l'applicazione.
/// </summary>
public class NotificationGuardTests
{
    [Fact]
    public void ARefusedNotification_IsReported_NotThrown()
    {
        var shown = NotificationGuard.TryShow(() => throw new InvalidOperationException("Show notification failed."));

        Assert.False(shown);
    }

    [Fact]
    public void AShownNotification_IsReportedAsShown()
    {
        var called = false;

        Assert.True(NotificationGuard.TryShow(() => called = true));
        Assert.True(called);
    }
}
