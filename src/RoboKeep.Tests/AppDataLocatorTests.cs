using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class AppDataLocatorTests
{
    [Fact]
    public void PortableFlagPresent_UsesExeDir()
    {
        var root = AppDataLocator.ResolveDataRoot(
            exeDir: @"C:\Apps\RoboKeep", appDataDir: @"C:\Users\me\AppData\Roaming", portableFlagPresent: true);
        Assert.Equal(@"C:\Apps\RoboKeep", root);
    }

    [Fact]
    public void NoFlag_UsesAppDataRoboKeep()
    {
        var root = AppDataLocator.ResolveDataRoot(
            exeDir: @"C:\Apps\RoboKeep", appDataDir: @"C:\Users\me\AppData\Roaming", portableFlagPresent: false);
        Assert.Equal(@"C:\Users\me\AppData\Roaming\RoboKeep", root);
    }
}
