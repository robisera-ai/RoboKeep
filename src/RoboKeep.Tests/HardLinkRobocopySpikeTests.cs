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

    [Fact]
    public void Robocopy_Update_BreaksHardLink_PreservingOldVersionInSnapshot()
    {
        var source = Path.Combine(_root, "src");
        var dest = Path.Combine(_root, "dest");
        var snap = Path.Combine(_root, "snap");
        Directory.CreateDirectory(source);

        // v1 nella sorgente, mirror in dest
        File.WriteAllText(Path.Combine(source, "f.txt"), "v1");
        Robocopy(source, dest);
        Assert.Equal("v1", File.ReadAllText(Path.Combine(dest, "f.txt")));

        // snapshot: hard-link di dest\f.txt
        Directory.CreateDirectory(snap);
        HardLink.Create(Path.Combine(snap, "f.txt"), Path.Combine(dest, "f.txt"));
        Assert.Equal("v1", File.ReadAllText(Path.Combine(snap, "f.txt")));

        // la sorgente cambia a v2, rimirror in dest
        File.WriteAllText(Path.Combine(source, "f.txt"), "v2");
        Robocopy(source, dest);

        // ASSUNZIONE CRITICA: dest aggiornato a v2, lo snapshot conserva v1 (hard-link rotto da robocopy)
        Assert.Equal("v2", File.ReadAllText(Path.Combine(dest, "f.txt")));
        Assert.Equal("v1", File.ReadAllText(Path.Combine(snap, "f.txt")));
    }
}
