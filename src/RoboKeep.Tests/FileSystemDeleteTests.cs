using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class FileSystemDeleteTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RbcDel_" + Guid.NewGuid().ToString("N"));

    public FileSystemDeleteTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (!Directory.Exists(_root)) return;
        foreach (var i in new DirectoryInfo(_root).GetFileSystemInfos("*", SearchOption.AllDirectories))
            if ((i.Attributes & FileAttributes.ReadOnly) != 0)
                i.Attributes &= ~FileAttributes.ReadOnly;
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void DeleteDirectory_RemovesTree_WithReadOnlyFileInSubfolder()
    {
        var dir = Path.Combine(_root, "snap");
        Directory.CreateDirectory(Path.Combine(dir, "sub"));
        var f = Path.Combine(dir, "sub", "ro.txt");
        File.WriteAllText(f, "x");
        File.SetAttributes(f, FileAttributes.ReadOnly);

        FileSystemDelete.DeleteDirectory(dir);

        Assert.False(Directory.Exists(dir)); // cancellata nonostante il file read-only annidato
    }

    [Fact]
    public void DeleteFile_PreservesReadOnly_OnSurvivingHardLink()
    {
        var a = Path.Combine(_root, "a.txt");
        var b = Path.Combine(_root, "b.txt");
        File.WriteAllText(a, "shared");
        Assert.True(HardLink.TryCreate(b, a));          // b e a puntano allo stesso inode
        File.SetAttributes(a, FileAttributes.ReadOnly); // attributo ReadOnly sull'inode condiviso

        FileSystemDelete.DeleteFile(a);                 // cancella il NOME 'a' senza toccare l'inode

        Assert.False(File.Exists(a));
        Assert.True(File.Exists(b));
        Assert.True(new FileInfo(b).Attributes.HasFlag(FileAttributes.ReadOnly)); // b conserva il ReadOnly
    }

    [Fact]
    public void DeleteDirectory_Missing_DoesNotThrow()
        => FileSystemDelete.DeleteDirectory(Path.Combine(_root, "non-esiste"));
}
