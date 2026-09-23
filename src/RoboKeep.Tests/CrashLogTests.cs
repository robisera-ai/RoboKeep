using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class CrashLogTests
{
    [Fact]
    public void Write_AppendsDateVersionContextAndStack()
    {
        var root = Path.Combine(Path.GetTempPath(), "robokeep-crashlog-" + Guid.NewGuid().ToString("N"));
        try
        {
            var ex = new InvalidOperationException("prova crash");
            var first = CrashLog.Write(root, ex, "UI");
            var second = CrashLog.Write(root, new ArgumentException("seconda"), null);

            Assert.Equal(Path.Combine(root, CrashLog.FileName), first);
            Assert.Equal(first, second);
            var text = File.ReadAllText(first!);
            Assert.Contains("RoboKeep ", text);
            Assert.Contains("— UI", text);
            Assert.Contains("InvalidOperationException: prova crash", text);
            Assert.Contains("ArgumentException: seconda", text);
            Assert.Equal(2, text.Split("===== ").Length - 1);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void Write_UnwritableRoot_ReturnsNullWithoutThrowing()
    {
        // Un percorso con caratteri non validi: CreateDirectory fallisce, il log no.
        var result = CrashLog.Write("\0invalid", new Exception("x"));
        Assert.Null(result);
    }
}
