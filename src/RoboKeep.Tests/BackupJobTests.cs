using System.Text.Json;
using RoboKeep.Core.Models;

namespace RoboKeep.Tests;

public class BackupJobUseVssTests
{
    [Fact]
    public void UseVss_DefaultsToFalse()
        => Assert.False(new BackupJob().UseVss);

    [Fact]
    public void UseVss_RoundTripsThroughJson()
    {
        var job = new BackupJob { Name = "x", UseVss = true };
        var json = JsonSerializer.Serialize(job);
        var back = JsonSerializer.Deserialize<BackupJob>(json)!;
        Assert.True(back.UseVss);
    }
}

public class BackupJobV14DefaultsTests
{
    [Fact]
    public void NewProperties_HaveSafeDefaults()
    {
        var j = new BackupJob();
        Assert.Equal(ScheduleKind.None, j.Schedule);
        Assert.Equal("21:00", j.ScheduleTime);
        Assert.Equal(DayOfWeek.Monday, j.ScheduleWeekDay);
        Assert.Equal(1, j.ScheduleMonthDay);
        Assert.False(j.VerifyAfterRun);
        Assert.Equal(0, j.InterPacketGapMs);
    }

    [Fact]
    public void NewProperties_RoundTripThroughJson()
    {
        var j = new BackupJob
        {
            Name = "x", Schedule = ScheduleKind.Weekly, ScheduleTime = "07:30",
            ScheduleWeekDay = DayOfWeek.Sunday, ScheduleMonthDay = 15,
            VerifyAfterRun = true, InterPacketGapMs = 20,
        };
        var back = JsonSerializer.Deserialize<BackupJob>(JsonSerializer.Serialize(j))!;
        Assert.Equal(ScheduleKind.Weekly, back.Schedule);
        Assert.Equal("07:30", back.ScheduleTime);
        Assert.Equal(DayOfWeek.Sunday, back.ScheduleWeekDay);
        Assert.Equal(15, back.ScheduleMonthDay);
        Assert.True(back.VerifyAfterRun);
        Assert.Equal(20, back.InterPacketGapMs);
    }
}

public class EmailSettingsDefaultsTests
{
    [Fact]
    public void Defaults_AreSecure()
    {
        var e = new EmailSettings();
        Assert.True(e.UseSsl);
        Assert.Equal(587, e.SmtpPort);
    }
}
