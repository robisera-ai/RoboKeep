using System.Diagnostics;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

/// <summary>
/// Prova sulla macchina reale: l'API COM dell'Utilita' di pianificazione non si puo' simulare,
/// e un elenco che torna sempre vuoto (il ripiego di List su errore) sarebbe indistinguibile
/// da un catalogo funzionante ma senza attivita'. Si crea davvero un'attivita' di prova con
/// schtasks, la si cerca, la si cancella con il catalogo e si controlla che sia sparita.
/// Se la creazione non riesce (criteri di sicurezza, account senza permessi) il test si
/// ferma senza fallire: sarebbe un rosso dovuto all'ambiente, non al codice.
/// </summary>
public class TaskSchedulerCatalogTests
{
    [Fact]
    public void List_FindsTheTask_AndDelete_RemovesIt()
    {
        var name = $"RoboKeep - _prova_{Guid.NewGuid():N}";
        if (!Schtasks("/Create", "/F", "/SC", "DAILY", "/ST", "23:59", "/TN", name, "/TR", "cmd.exe /c exit"))
            return; // ambiente senza permessi sull'Utilita' di pianificazione: niente da provare

        try
        {
            var listed = TaskSchedulerCatalog.List();
            var mine = listed.SingleOrDefault(t => t.Name == name);
            Assert.NotNull(mine);
            Assert.EndsWith("cmd.exe", mine!.Command, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("/c exit", mine.Arguments);
            // Appena creata: nessuna esecuzione alle spalle, ma una prossima gia' fissata.
            Assert.Null(mine.LastRun);
            Assert.NotNull(mine.NextRun);

            Assert.True(TaskSchedulerCatalog.Delete(name));
            Assert.DoesNotContain(TaskSchedulerCatalog.List(), t => t.Name == name);
        }
        finally
        {
            Schtasks("/Delete", "/F", "/TN", name);
        }
    }

    [Fact]
    public void Delete_RefusesATaskThatIsNotRoboKeep()
        => Assert.False(TaskSchedulerCatalog.Delete("OneDrive Reporting Task"));

    [Theory]
    [InlineData(@"RoboKeep_x\..\Microsoft\Windows\UpdateOrchestrator\Reboot")]
    [InlineData("RoboKeep_x/../Foo")]
    [InlineData(@"RoboKeep - Documenti\Sotto")]
    public void Delete_RefusesANameThatEscapesTheRootFolder(string name)
    {
        // I nomi delle attivita' sono segmenti di percorso: col prefisso giusto e una barra
        // si arriverebbe a cancellare le attivita' di Windows. Il rifiuto arriva prima di
        // qualunque chiamata COM.
        Assert.True(ScheduledTaskInfo.IsRoboKeep(name)); // il prefisso da solo non basta
        Assert.False(TaskSchedulerCatalog.Delete(name));
    }

    [Fact]
    public void List_ReturnsOnlyRoboKeepTasks()
        => Assert.All(TaskSchedulerCatalog.List(), t => Assert.True(ScheduledTaskInfo.IsRoboKeep(t.Name)));

    private static bool Schtasks(params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);
        using var p = Process.Start(psi);
        if (p is null) return false;
        p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit();
        return p.ExitCode == 0;
    }
}
