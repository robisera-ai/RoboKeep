using RoboKeep.Core;
using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

[Collection(CultureCollection.Name)]
public class EmailServiceTests
{
    private static JobResult Hw() => new()
    {
        JobName = "Progetti", Success = false, ExitCode = 16, HardwareError = true,
        HardwareErrorDetail = @"Win32 23: Copia del file in corso D:\x\f.dat",
        Status = "INTERROTTO", StartedAt = new DateTime(2026, 9, 22, 21, 0, 0),
    };

    [Fact]
    public void Subject_SaysHardwareError_NotGenericError()
    {
        Assert.Contains(CoreLoc.S("Email_HardwareError"), EmailService.BuildSubject(Hw()));
        Assert.DoesNotContain(CoreLoc.S("Email_HardwareError"), EmailService.BuildSubject(new JobResult { JobName = "J", Success = false }));
    }

    [Fact]
    public void Body_LeadsWithDetailAndAdvice_ForHardwareError()
    {
        var body = EmailService.BuildBody(Hw());
        var detailAt = body.IndexOf(@"D:\x\f.dat", StringComparison.Ordinal);
        var label = CoreLoc.S("Lbl_FilesCopied");
        var countsAt = body.IndexOf(label, StringComparison.Ordinal);
        Assert.True(countsAt >= 0, $"etichetta '{label}' assente dal corpo:\n{body}");
        Assert.True(detailAt >= 0 && detailAt < countsAt, "il dettaglio deve precedere i conteggi");
        Assert.Contains("SMART", body);                     // il consiglio azionabile c'e'
    }

    [Fact]
    public void Body_ForNotStartedJob_UsesTheSkipSentence_NotInterrupted()
    {
        var r = new JobResult { JobName = "J", Success = false, HardwareError = true, NotStarted = true,
            HardwareErrorDetail = "[disco] job NON eseguito: il disco E:\\ e' a riposo" };
        var body = EmailService.BuildBody(r);
        Assert.StartsWith("[disco] job NON eseguito", body);
        Assert.DoesNotContain(CoreLoc.S("Hw_Stop").Split('{')[0].Trim(), body); // niente prefisso "INTERROTTO"
        Assert.Contains(CoreLoc.S("Hw_Advice"), body);
    }

    [Fact]
    public void Body_IncludesHealthWarnings_WhenPresent()
    {
        var r = new JobResult { JobName = "J", Success = true };
        r.HealthWarnings.Add("[disco] ATTENZIONE: 7 blocchi danneggiati");
        Assert.Contains("7 blocchi danneggiati", EmailService.BuildBody(r));
    }

    [Fact]
    public void Body_ForNormalResult_HasNoHardwareBlock()
    {
        Assert.DoesNotContain(CoreLoc.S("Hw_Advice"), EmailService.BuildBody(new JobResult { JobName = "J", Success = true }));
    }

    [Fact]
    public void OnlyOnError_StillSends_WhenHealthWarningsPresent()
    {
        var settings = new EmailSettings { Enabled = true, OnlyOnError = true };

        var successNoWarnings = new JobResult { JobName = "J", Success = true };
        Assert.False(EmailService.ShouldSend(settings, successNoWarnings));

        var successWithWarning = new JobResult { JobName = "J", Success = true };
        successWithWarning.HealthWarnings.Add("[disco] ATTENZIONE");
        Assert.True(EmailService.ShouldSend(settings, successWithWarning));

        var failure = new JobResult { JobName = "J", Success = false };
        Assert.True(EmailService.ShouldSend(settings, failure));

        var disabled = new EmailSettings { Enabled = false, OnlyOnError = true };
        Assert.False(EmailService.ShouldSend(disabled, successWithWarning));
    }

    [Fact]
    public void OnlyOnError_NeverSends_ForASuccessfulPreview()
    {
        // Un'anteprima non copia niente: gli avvisi di salute che trova sono gli stessi del run
        // vero che li ha gia' mandati. Provare i filtri di un job non deve riempire la casella.
        var settings = new EmailSettings { Enabled = true, OnlyOnError = true };
        var preview = new JobResult { JobName = "J", Success = true, DryRun = true };
        preview.HealthWarnings.Add("[disco] ATTENZIONE");

        Assert.False(EmailService.ShouldSend(settings, preview));

        // Un'anteprima FALLITA resta una notizia: e' quello che l'anteprima serve a scoprire.
        var failedPreview = new JobResult { JobName = "J", Success = false, DryRun = true };
        Assert.True(EmailService.ShouldSend(settings, failedPreview));
    }
}
