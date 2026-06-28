using System.Diagnostics;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class HardLinkRobocopySpikeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RbcSpike_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static int Robocopy(string source, string dest, params string[] extra)
    {
        var psi = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "Robocopy.exe"),
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        psi.ArgumentList.Add(source); psi.ArgumentList.Add(dest);
        psi.ArgumentList.Add("/MIR");
        foreach (var e in extra) psi.ArgumentList.Add(e);
        using var p = Process.Start(psi)!;
        p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd();
        p.WaitForExit();
        return p.ExitCode;
    }

    // DOCUMENTA il comportamento reale: robocopy modifica sul posto, trascinando l'hard-link.
    [Fact]
    public void Robocopy_ModifiesInPlace_DraggingHardLinkedSnapshotToNewVersion()
    {
        var source = Path.Combine(_root, "src");
        var dest = Path.Combine(_root, "dest");
        var snap = Path.Combine(_root, "snap");
        Directory.CreateDirectory(source);

        File.WriteAllText(Path.Combine(source, "f.txt"), "v1");
        Robocopy(source, dest);

        Directory.CreateDirectory(snap);
        HardLink.Create(Path.Combine(snap, "f.txt"), Path.Combine(dest, "f.txt"));

        File.WriteAllText(Path.Combine(source, "f.txt"), "v2");
        Robocopy(source, dest);

        Assert.Equal("v2", File.ReadAllText(Path.Combine(dest, "f.txt")));
        Assert.Equal("v2", File.ReadAllText(Path.Combine(snap, "f.txt")));
    }

    // VALIDA il meccanismo: cancellare il file dal nuovo snapshot prima di robocopy => robocopy
    // lo RICREA nuovo, e la vecchia versione resta nello snapshot precedente.
    [Fact]
    public void DeleteChangedThenRobocopy_RecreatesFile_PreservingOldVersionInPrevSnapshot()
    {
        var source = Path.Combine(_root, "src2");
        var prev = Path.Combine(_root, "prev");
        var curr = Path.Combine(_root, "curr");
        Directory.CreateDirectory(source);

        File.WriteAllText(Path.Combine(source, "f.txt"), "v1");
        Robocopy(source, prev);

        Directory.CreateDirectory(curr);
        HardLink.Create(Path.Combine(curr, "f.txt"), Path.Combine(prev, "f.txt"));

        File.WriteAllText(Path.Combine(source, "f.txt"), "v2");
        File.Delete(Path.Combine(curr, "f.txt"));

        Robocopy(source, curr);

        Assert.Equal("v2", File.ReadAllText(Path.Combine(curr, "f.txt")));
        Assert.Equal("v1", File.ReadAllText(Path.Combine(prev, "f.txt")));
    }
}
