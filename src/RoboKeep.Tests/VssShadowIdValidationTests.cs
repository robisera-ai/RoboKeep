using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class VssShadowIdValidationTests
{
    [Theory]
    [InlineData("{1FC64F91-4A9E-4D7C-9E1B-123456789ABC}")]
    public void ValidWmiIds_Pass(string id)
        => Assert.True(VssSessionProtocol.IsValidShadowId(id));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("x' OR ID <> 'x")]
    [InlineData("{1FC64F91-4A9E-4D7C-9E1B-123456789ABC}' OR ID <> '{x}")]
    [InlineData("1FC64F91-4A9E-4D7C-9E1B-123456789ABC")] // senza graffe
    [InlineData("{not-a-guid-at-all-##############}")]
    public void InvalidOrMalicious_Rejected(string? id)
        => Assert.False(VssSessionProtocol.IsValidShadowId(id));
}
