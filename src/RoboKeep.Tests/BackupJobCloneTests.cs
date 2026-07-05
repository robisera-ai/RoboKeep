using System.Collections;
using System.Reflection;
using RoboKeep.Core.Models;

namespace RoboKeep.Tests;

public class BackupJobCloneTests
{
    private static readonly BackupJob Baseline = new();

    /// <summary>Genera un valore non-default per il tipo dato: se questo test fallisce per un
    /// tipo nuovo, aggiungere qui il caso — MAI indebolire l'assert.</summary>
    private static object NonDefault(PropertyInfo p, int seed) => p.PropertyType switch
    {
        var t when t == typeof(string) => $"v{seed}",
        // Inverso del default reale: anche Mirror/Enabled (default true) vengono forzati.
        var t when t == typeof(bool) => !(bool)p.GetValue(Baseline)!,
        var t when t == typeof(int) => 40 + seed,
        var t when t == typeof(List<string>) => new List<string> { $"item{seed}" },
        var t when t.IsEnum => t.GetEnumValues().Cast<object>().Last(),
        _ => throw new NotSupportedException(
            $"Tipo {p.PropertyType} della proprietà {p.Name}: aggiungere un caso al test."),
    };

    private static BackupJob FullyPopulated()
    {
        var job = new BackupJob();
        var i = 0;
        foreach (var p in typeof(BackupJob).GetProperties().Where(p => p.CanWrite))
            p.SetValue(job, NonDefault(p, i++));
        return job;
    }

    [Fact]
    public void Clone_PreservesEveryWritableProperty()
    {
        var original = FullyPopulated();
        var clone = original.Clone();
        foreach (var p in typeof(BackupJob).GetProperties().Where(p => p.CanWrite))
        {
            var a = p.GetValue(original);
            var b = p.GetValue(clone);
            if (a is IEnumerable ea && a is not string)
                Assert.Equal(ea.Cast<object>(), ((IEnumerable)b!).Cast<object>());
            else
                Assert.Equal(a, b);
        }
    }

    [Fact]
    public void CopyInto_PreservesEveryWritableProperty_AndDoesNotShareLists()
    {
        var source = FullyPopulated();
        var target = new BackupJob();
        source.CopyInto(target);
        Assert.Equal(source.Name, target.Name);
        Assert.Equal(source.ExcludeFiles, target.ExcludeFiles);
        Assert.NotSame(source.ExcludeFiles, target.ExcludeFiles); // liste indipendenti
        foreach (var p in typeof(BackupJob).GetProperties().Where(p => p.CanWrite))
        {
            var a = p.GetValue(source);
            var b = p.GetValue(target);
            if (a is IEnumerable ea && a is not string)
                Assert.Equal(ea.Cast<object>(), ((IEnumerable)b!).Cast<object>());
            else
                Assert.Equal(a, b);
        }
    }
}
