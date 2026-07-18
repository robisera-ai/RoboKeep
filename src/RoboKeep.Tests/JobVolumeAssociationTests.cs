using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class JobVolumeAssociationTests
{
    private const string FakeVolumeId = @"\\?\Volume{aaaaaaaa-1111-2222-3333-444444444444}\";

    [Fact]
    public void ToCurrentDisk_LocalPath_AssociatesToRealVolume()
    {
        var job = new BackupJob { Name = "j", Source = @"C:\s", Destination = Path.GetTempPath() };
        JobVolumeAssociation.ToCurrentDisk(job);
        Assert.StartsWith(@"\\?\Volume{", job.DestinationVolumeId!);
    }

    [Fact]
    public void ToCurrentDisk_UncPath_ClearsAssociation()
    {
        // Una share di rete non ha un volume removibile: l'associazione va azzerata, non
        // lasciata a un id stantio (che il runner scambierebbe per disco atteso).
        var job = new BackupJob
        {
            Name = "j", Source = @"C:\s", Destination = @"\\server\share\backup",
            DestinationVolumeId = FakeVolumeId, DestinationVolumeLabel = "STANTIO",
        };
        JobVolumeAssociation.ToCurrentDisk(job);
        Assert.Null(job.DestinationVolumeId);
        Assert.Null(job.DestinationVolumeLabel);
    }

    [Fact]
    public void ToCurrentDisk_AbsentDriveLetter_ClearsAssociation()
    {
        var used = DriveInfo.GetDrives().Select(d => char.ToUpperInvariant(d.Name[0])).ToHashSet();
        var free = "ZYXWVU".FirstOrDefault(c => !used.Contains(c));
        if (free == '\0') return;
        var job = new BackupJob
        {
            Name = "j", Source = @"C:\s", Destination = $@"{free}:\backup",
            DestinationVolumeId = FakeVolumeId, DestinationVolumeLabel = "STANTIO",
        };
        JobVolumeAssociation.ToCurrentDisk(job);
        Assert.Null(job.DestinationVolumeId);
    }
}
