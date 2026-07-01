using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class LongPathTests
{
    [Fact]
    public void Extended_DriveLetterPath_GetsPrefix()
        => Assert.Equal(@"\\?\E:\Backup\Desktop\file.txt", LongPath.Extended(@"E:\Backup\Desktop\file.txt"));

    [Fact]
    public void Extended_UncPath_GetsUncPrefix()
        => Assert.Equal(@"\\?\UNC\server\share\file.txt", LongPath.Extended(@"\\server\share\file.txt"));

    [Fact]
    public void Extended_AlreadyPrefixed_IsUnchanged()
        => Assert.Equal(@"\\?\E:\x", LongPath.Extended(@"\\?\E:\x"));

    [Fact]
    public void Extended_EmptyOrNull_IsReturnedAsIs()
    {
        Assert.Equal("", LongPath.Extended(""));
        Assert.Null(LongPath.Extended(null!));
    }

    [Fact]
    public void Extended_NormalizesDotSegments()
        => Assert.Equal(@"\\?\E:\a\c", LongPath.Extended(@"E:\a\b\..\c"));
}
