using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class CredentialServiceTests
{
    [Theory]
    [InlineData(CredentialProtectionScope.Machine)]
    [InlineData(CredentialProtectionScope.User)]
    public void ProtectUnprotect_RoundTrips(CredentialProtectionScope scope)
    {
        var enc = CredentialService.ProtectWith("segreto123!", scope);
        Assert.NotEqual("segreto123!", enc);
        Assert.Equal("segreto123!", CredentialService.UnprotectWith(enc, scope));
    }

    [Fact]
    public void Migration_FromMachineToUser_PreservesSecret()
    {
        var encMachine = CredentialService.ProtectWith("pw", CredentialProtectionScope.Machine);
        var plain = CredentialService.UnprotectWith(encMachine, CredentialProtectionScope.Machine);
        var encUser = CredentialService.ProtectWith(plain, CredentialProtectionScope.User);

        Assert.Equal("pw", CredentialService.UnprotectWith(encUser, CredentialProtectionScope.User));
    }

    [Fact]
    public void Instance_UsesConfiguredScope()
    {
        var svc = new CredentialService(CredentialProtectionScope.User);
        var enc = svc.Protect("x");
        Assert.Equal("x", svc.Unprotect(enc));
        Assert.Equal("x", CredentialService.UnprotectWith(enc, CredentialProtectionScope.User));
    }

    [Fact]
    public void Unprotect_EmptyString_ReturnsEmpty()
    {
        Assert.Equal("", CredentialService.UnprotectWith("", CredentialProtectionScope.Machine));
    }
}
