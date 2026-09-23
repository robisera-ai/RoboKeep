using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class NetworkShareTests
{
    [Theory]
    [InlineData(@"\\nas01\backup$\Reparti\Acquisti", @"\\nas01\backup$")]
    [InlineData(@"\\nas01\backup$", @"\\nas01\backup$")]
    [InlineData(@"  \\nas01\backup$\  ", @"\\nas01\backup$")]
    [InlineData(@"\\nas01", null)]          // manca la share
    [InlineData(@"Z:\Backup", null)]        // unita' mappata: non e' UNC
    [InlineData(@"D:\Documenti", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void TryGetShare_ExtractsServerAndShare(string? path, string? expected)
        => Assert.Equal(expected, NetworkShare.TryGetShare(path));

    [Fact]
    public void FirstShare_PrefersDestination()
    {
        Assert.Equal(@"\\dst\d", NetworkShare.FirstShare(@"\\dst\d\x", @"\\src\s\y"));
        Assert.Equal(@"\\src\s", NetworkShare.FirstShare(@"E:\Backup", @"\\src\s\y"));
        Assert.Null(NetworkShare.FirstShare(@"E:\Backup", @"D:\Documenti"));
    }

    [Fact]
    public void FindCredential_MatchesShareOrServer_CaseInsensitive()
    {
        var creds = new[]
        {
            new CredentialEntry { Id = "ufficio", Host = @"\\NAS01\backup$" },
            new CredentialEntry { Id = "casa", Host = "nas-casa" },
        };
        Assert.Equal("ufficio", NetworkShare.FindCredential(creds, @"\\nas01\backup$")!.Id);
        Assert.Equal("casa", NetworkShare.FindCredential(creds, @"\\nas-casa\foto")!.Id);
        Assert.Null(NetworkShare.FindCredential(creds, @"\\altro\share"));
    }

    [Fact]
    public void SuggestId_UsesServer_AndAvoidsCollisions()
    {
        Assert.Equal("nas01", NetworkShare.SuggestId(@"\\nas01\backup$", Array.Empty<string>()));
        Assert.Equal("nas01-2", NetworkShare.SuggestId(@"\\nas01\backup$", new[] { "NAS01" }));
        Assert.Equal("nas01-3", NetworkShare.SuggestId(@"\\nas01\backup$", new[] { "nas01", "nas01-2" }));
    }
}
