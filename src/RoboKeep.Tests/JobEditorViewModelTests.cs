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

    private const string FakeVolumeId = @"\\?\Volume{aaaaaaaa-1111-2222-3333-444444444444}\";

    [Fact]
    public void SavingWithoutTouchingDestination_NeverReassociatesVolume()
    {
        // La falla che questo test blocca: aprire l'editor col disco sbagliato inserito e
        // salvare una modifica qualsiasi non deve riassociare il job al disco sbagliato.
        var job = new BackupJob
        {
            Name = "j", Source = @"C:\s", Destination = @"C:\d",
            DestinationVolumeId = FakeVolumeId, DestinationVolumeLabel = "ALTRO",
        };
        var vm = Vm(job);

        vm.Name = "nome nuovo";
        vm.Mirror = !vm.Mirror;

        Assert.Equal(FakeVolumeId, job.DestinationVolumeId);
        Assert.Equal("ALTRO", job.DestinationVolumeLabel);
    }

    [Fact]
    public void ChangingDestination_ReassociatesToNewVolume()
    {
        var job = new BackupJob
        {
            Name = "j", Source = @"C:\s", Destination = @"C:\d",
            DestinationVolumeId = FakeVolumeId, DestinationVolumeLabel = "ALTRO",
        };
        var vm = Vm(job);

        vm.Destination = @"C:\Windows";

        Assert.NotEqual(FakeVolumeId, job.DestinationVolumeId);
        Assert.StartsWith(@"\\?\Volume{", job.DestinationVolumeId!);
    }

    [Fact]
    public void UseCurrentVolume_AssociatesToTheConnectedDisk()
    {
        var job = new BackupJob
        {
            Name = "j", Source = @"C:\s", Destination = @"C:\",
            DestinationVolumeId = FakeVolumeId, DestinationVolumeLabel = "ALTRO",
        };
        var vm = Vm(job);
        Assert.True(vm.VolumeMismatch); // il disco memorizzato non e' quello presente

        vm.UseCurrentVolume();

        Assert.False(vm.VolumeMismatch);
        Assert.StartsWith(@"\\?\Volume{", job.DestinationVolumeId!);
    }

    [Fact]
    public void UnassociatedLocalJob_OffersProtection()
    {
        // I job creati prima della v1.5 non hanno associazione: l'editor deve mostrare la riga
        // e offrire "Proteggi" (col disco presente), non nascondere tutto lasciando l'utente
        // senza modo di proteggerli.
        var job = new BackupJob { Name = "j", Source = @"C:\s", Destination = Path.GetTempPath() };
        var vm = Vm(job);

        Assert.False(vm.HasVolume);
        Assert.True(vm.ShowVolumeRow);       // la temp e' un disco locale presente
        Assert.True(vm.VolumeActionVisible);  // pulsante "Proteggi" disponibile
        Assert.False(vm.VolumeMismatch);      // non associato: nessun avviso di disco sbagliato

        vm.UseCurrentVolume();

        Assert.True(vm.HasVolume);
        Assert.False(vm.VolumeActionVisible); // ora protetto e disco giusto: niente pulsante
    }

    [Fact]
    public void NetworkJob_HidesVolumeRow()
    {
        // Una share di rete non ha un volume removibile da proteggere: niente riga "Disco".
        var job = new BackupJob { Name = "j", Source = @"C:\s", Destination = @"\\server\share\backup" };
        var vm = Vm(job);
        Assert.False(vm.ShowVolumeRow);
    }

    [Fact]
    public void UnassociatedJobOnAbsentDrive_ShowsRowAndHint_NoButton()
    {
        // Cambiare destinazione verso un disco locale ora staccato non deve nascondere la riga:
        // l'utente deve vedere che la protezione non è attiva, invece di un azzeramento silenzioso.
        var used = DriveInfo.GetDrives().Select(d => char.ToUpperInvariant(d.Name[0])).ToHashSet();
        var free = "ZYXWVU".FirstOrDefault(c => !used.Contains(c));
        if (free == '\0') return;
        var job = new BackupJob { Name = "j", Source = @"C:\s", Destination = $@"{free}:\backup" };
        var vm = Vm(job);

        Assert.True(vm.ShowVolumeRow);        // la riga resta visibile...
        Assert.True(vm.ShowConnectDiskHint);  // ...con l'avviso "disco non collegato"
        Assert.False(vm.VolumeActionVisible); // nessun disco presente: niente da associare ora
    }
}
