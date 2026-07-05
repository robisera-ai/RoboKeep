using RoboKeep.Core.Models;
using RoboKeep.ViewModels;

namespace RoboKeep.Tests;

public class JobEditorViewModelTests
{
    private static JobEditorViewModel Vm(BackupJob? job = null) =>
        new(job ?? new BackupJob(), Array.Empty<CredentialEntry>());

    [Fact]
    public void ScheduleWeekDayIndex_RoundTrips_AllSevenDays()
    {
        var vm = Vm();
        for (var i = 0; i < 7; i++)
        {
            vm.ScheduleWeekDayIndex = i;
            Assert.Equal(i, vm.ScheduleWeekDayIndex);
        }
        // Ordine europeo: indice 0 = lunedì, indice 6 = domenica.
        vm.ScheduleWeekDayIndex = 0;
        Assert.Equal(DayOfWeek.Monday, vm.Job.ScheduleWeekDay);
        vm.ScheduleWeekDayIndex = 6;
        Assert.Equal(DayOfWeek.Sunday, vm.Job.ScheduleWeekDay);
    }

    [Fact]
    public void ScheduleIndex_MatchesEnumOrder()
    {
        var vm = Vm();
        vm.ScheduleIndex = 3;
        Assert.Equal(ScheduleKind.Monthly, vm.Job.Schedule);
        vm.ScheduleIndex = 0;
        Assert.Equal(ScheduleKind.None, vm.Job.Schedule);
    }

    [Fact]
    public void Validate_RejectsBadScheduleTime_AndQuotedNames()
    {
        var vm = Vm(new BackupJob { Name = "ok", Source = @"C:\s", Destination = @"D:\d" });
        vm.ScheduleIndex = 1;
        vm.ScheduleTime = "banana";
        Assert.NotNull(vm.Validate());
        vm.ScheduleTime = "21:30";
        Assert.Null(vm.Validate());
        vm.Name = "job \"cattivo\"";
        Assert.NotNull(vm.Validate());
    }
}
