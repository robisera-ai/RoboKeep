using RoboKeep.ViewModels;

namespace RoboKeep.Tests;

/// <summary>La procedura guidata non lascia creare un job che scriverebbe nella cartella di un altro.</summary>
public class JobWizardViewModelTests
{
    [Fact]
    public void ADestinationSharedWithAnotherJob_BlocksTheFirstStep_AndSaysWhichJob()
    {
        var vm = new JobWizardViewModel(new[] { new OtherJob("3xDocumenti", @"E:\Backup\Documenti") })
        {
            Name = "Nuovo",
            Source = @"C:\Altro",
            Destination = @"E:\Backup\Documenti",
        };

        Assert.False(vm.CanGoNext);
        Assert.Contains("3xDocumenti", vm.DestOverlapMessage);

        vm.Destination = @"E:\Backup\Nuovo";
        Assert.True(vm.CanGoNext);
        Assert.Null(vm.DestOverlapMessage);
    }

    [Fact]
    public void TheSameFolderOnTheDiskNowPluggedIn_IsAllowed_IfTheOtherJobIsTiedToAnotherDisk()
    {
        // Il job nuovo viene legato al disco collegato adesso: se l'altro job e' legato a un disco
        // diverso, la stessa cartella non e' un conflitto (rotazione dei dischi).
        var vm = new JobWizardViewModel(
            new[] { new OtherJob("provadiff", @"E:\Backup\Provadiff", @"\\?\Volume{a}\") },
            volumeOf: _ => @"\\?\Volume{b}\")
        {
            Name = "WD",
            Source = @"D:\Provadiff",
            Destination = @"E:\Backup\Provadiff",
        };

        Assert.True(vm.CanGoNext);
        Assert.Null(vm.DestOverlapMessage);
    }
}
