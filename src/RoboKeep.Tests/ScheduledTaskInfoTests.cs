using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class ScheduledTaskInfoTests
{
    private const string Exe = @"C:\Programmi\RoboKeep\RoboKeep.exe";

    private static ScheduledTaskInfo Task(string name, string command = Exe) =>
        new(name, null, null, null, 3, command, "--job \"x\"");

    [Fact]
    public void IsRoboKeep_AcceptsBothPrefixes()
    {
        Assert.True(ScheduledTaskInfo.IsRoboKeep("RoboKeep - Documenti"));
        Assert.True(ScheduledTaskInfo.IsRoboKeep("RoboKeep_AvviaTutti"));
    }

    [Fact]
    public void IsRoboKeep_RejectsOtherTasks()
    {
        Assert.False(ScheduledTaskInfo.IsRoboKeep("OneDrive Reporting Task"));
        Assert.False(ScheduledTaskInfo.IsRoboKeep(""));
        Assert.False(ScheduledTaskInfo.IsRoboKeep(null));
    }

    [Fact]
    public void JobNameOf_ReturnsTheSanitizedPart()
        => Assert.Equal("Documenti", ScheduledTaskInfo.JobNameOf("RoboKeep - Documenti"));

    [Fact]
    public void JobNameOf_IsNullForTheGlobalTask()
        => Assert.Null(ScheduledTaskInfo.JobNameOf("RoboKeep_AvviaTutti"));

    [Fact]
    public void Classify_JobPresent_IsJob()
        => Assert.Equal(TaskLink.Job,
            ScheduledTaskInfo.Classify(Task("RoboKeep - Documenti"), new[] { "Documenti" }, Exe));

    [Fact]
    public void Classify_RunAll_IsRunAll()
        => Assert.Equal(TaskLink.RunAll,
            ScheduledTaskInfo.Classify(Task("RoboKeep_AvviaTutti"), new[] { "Documenti" }, Exe));

    [Fact]
    public void Classify_RunAll_IsRunAll_EvenWithoutJobs()
        => Assert.Equal(TaskLink.RunAll,
            ScheduledTaskInfo.Classify(Task("RoboKeep_AvviaTutti"), Array.Empty<string>(), Exe));

    [Fact]
    public void Classify_NoJobWithThatName_IsOrphan()
        => Assert.Equal(TaskLink.OrphanNoJob,
            ScheduledTaskInfo.Classify(Task("RoboKeep - Vecchio"), new[] { "Documenti" }, Exe));

    [Fact]
    public void Classify_OtherExecutable_IsOrphanOtherExe()
        => Assert.Equal(TaskLink.OrphanOtherExe,
            ScheduledTaskInfo.Classify(Task("RoboKeep - Documenti", @"D:\Portatile\RoboKeep.exe"),
                new[] { "Documenti" }, Exe));

    [Fact]
    public void Classify_SanitizedJobName_StillMatchesItsJob()
    {
        // Il job si chiama "My/Job", l'attivita' "RoboKeep - My_Job": il confronto deve passare
        // da SchtasksArgs.TaskName, altrimenti un job con una barra risulterebbe sempre orfano.
        var task = Task(SchtasksArgs.TaskName("My/Job"));
        Assert.Equal("RoboKeep - My_Job", task.Name);
        Assert.Equal(TaskLink.Job, ScheduledTaskInfo.Classify(task, new[] { "My/Job" }, Exe));
    }

    [Fact]
    public void MatchingJobName_ReturnsTheRealJobName_NotTheSanitizedSuffix()
    {
        // La riga a video deve dire «Foto/2026», il nome che l'utente ha scritto, non
        // «Foto_2026» che e' solo come Windows puo' chiamare l'attivita'.
        var task = Task(SchtasksArgs.TaskName("Foto/2026"));
        Assert.Equal("Foto/2026", ScheduledTaskInfo.MatchingJobName(task, new[] { "Altro", "Foto/2026" }));
    }

    [Fact]
    public void MatchingJobName_IsNullWhenNoJobGeneratesThatTask()
        => Assert.Null(ScheduledTaskInfo.MatchingJobName(Task("RoboKeep - Vecchio"), new[] { "Documenti" }));

    [Fact]
    public void Classify_SamePathDifferentCase_IsStillTheSameExecutable()
        => Assert.Equal(TaskLink.Job,
            ScheduledTaskInfo.Classify(Task("RoboKeep - Documenti", Exe.ToUpperInvariant()),
                new[] { "Documenti" }, Exe));

    [Fact]
    public void Classify_QuotedCommand_IsStillTheSameExecutable()
        => Assert.Equal(TaskLink.Job,
            ScheduledTaskInfo.Classify(Task("RoboKeep - Documenti", "\"" + Exe + "\""),
                new[] { "Documenti" }, Exe));

    [Fact]
    public void Classify_UnknownCurrentExe_DoesNotAccuseTheTask()
    {
        // Senza un percorso da confrontare (Environment.ProcessPath nullo) non si puo' dire
        // che l'attivita' punti altrove: meglio nessun verdetto che un verdetto inventato.
        Assert.Equal(TaskLink.Job,
            ScheduledTaskInfo.Classify(Task("RoboKeep - Documenti", @"D:\Altro\RoboKeep.exe"),
                new[] { "Documenti" }, null));
    }
}
