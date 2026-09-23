using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class SchtasksArgsTests
{
    private static BackupJob Job(ScheduleKind kind) => new()
    {
        Name = "Documenti",
        Schedule = kind,
        ScheduleTime = "21:30",
        ScheduleWeekDay = DayOfWeek.Sunday,
        ScheduleMonthDay = 15,
    };

    [Fact]
    public void TaskName_HasPrefix_AndSanitizesInvalidChars()
        => Assert.Equal("RoboKeep - My_Job", SchtasksArgs.TaskName(@"My\Job"));

    [Fact]
    public void Xml_Daily_ContainsScheduleByDayAndTime()
    {
        var xml = SchtasksArgs.BuildTaskXml(Job(ScheduleKind.Daily), @"C:\app\RoboKeep.exe");
        Assert.Contains("<ScheduleByDay>", xml);
        Assert.Contains("T21:30:00", xml);
        Assert.Contains(@"<Command>C:\app\RoboKeep.exe</Command>", xml);
        Assert.Contains("--job &quot;Documenti&quot;", xml);
    }

    [Fact]
    public void Xml_Weekly_ContainsDayElement_NotLocalizedText()
    {
        var xml = SchtasksArgs.BuildTaskXml(Job(ScheduleKind.Weekly), @"C:\app\RoboKeep.exe");
        Assert.Contains("<ScheduleByWeek>", xml);
        Assert.Contains("<Sunday />", xml);
        Assert.DoesNotContain("SUN", xml); // niente abbreviazioni localizzate stile /d di schtasks
    }

    [Fact]
    public void Xml_Monthly_ContainsDayOfMonthAndAllMonths()
    {
        var xml = SchtasksArgs.BuildTaskXml(Job(ScheduleKind.Monthly), @"C:\app\RoboKeep.exe");
        Assert.Contains("<ScheduleByMonth>", xml);
        Assert.Contains("<Day>15</Day>", xml);
        Assert.Contains("<January />", xml);
        Assert.Contains("<December />", xml);
    }

    [Fact]
    public void Xml_Monthly_LastDay_UsesLastNotANumber()
    {
        // "Ultimo giorno del mese": Windows lo esprime con <Day>Last</Day>, e cosi' scatta il 28,
        // 29, 30 o 31 a seconda del mese, invece di saltare i mesi corti.
        var job = Job(ScheduleKind.Monthly);
        job.ScheduleLastDayOfMonth = true;
        var xml = SchtasksArgs.BuildTaskXml(job, @"C:\app\RoboKeep.exe");
        Assert.Contains("<Day>Last</Day>", xml);
        Assert.DoesNotContain("<Day>15</Day>", xml);
    }

    [Fact]
    public void Xml_BadTime_FallsBackTo2100()
    {
        var job = Job(ScheduleKind.Daily);
        job.ScheduleTime = "banana";
        var xml = SchtasksArgs.BuildTaskXml(job, @"C:\app\RoboKeep.exe");
        Assert.Contains("T21:00:00", xml);
    }

    [Fact]
    public void Xml_JobNameWithAmpersand_IsEscaped()
    {
        var job = Job(ScheduleKind.Daily);
        job.Name = "Docs & Foto";
        var xml = SchtasksArgs.BuildTaskXml(job, @"C:\app\RoboKeep.exe");
        Assert.Contains("Docs &amp; Foto", xml);
    }
}
