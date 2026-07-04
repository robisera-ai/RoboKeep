using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class VssPathMapperTests
{
    [Theory]
    [InlineData(@"C:\Users\robi\Documenti", @"C:\")]
    [InlineData(@"e:\Backup", @"E:\")]
    public void GetVolumeRoot_DriveLetterPath_ReturnsRoot(string path, string expected)
        => Assert.Equal(expected, VssPathMapper.GetVolumeRoot(path));

    [Theory]
    [InlineData(@"\\server\share\cartella")]
    [InlineData(@"relativo\cartella")]
    [InlineData("")]
    public void GetVolumeRoot_NonLocalOrRelative_ReturnsNull(string path)
        => Assert.Null(VssPathMapper.GetVolumeRoot(path));

    [Fact]
    public void MapToSnapshot_ReplacesVolumeWithLinkRoot()
        => Assert.Equal(@"D:\sess\source\Users\robi\Documenti",
            VssPathMapper.MapToSnapshot(@"C:\Users\robi\Documenti", @"D:\sess\source"));

    [Fact]
    public void MapToSnapshot_SourceIsVolumeRoot_ReturnsLinkRoot()
        => Assert.Equal(@"D:\sess\source",
            VssPathMapper.MapToSnapshot(@"C:\", @"D:\sess\source"));

    [Fact]
    public void MapToSnapshot_TrailingSlashOnSource_Normalized()
        => Assert.Equal(@"D:\sess\source\Users",
            VssPathMapper.MapToSnapshot(@"C:\Users\", @"D:\sess\source"));
}
