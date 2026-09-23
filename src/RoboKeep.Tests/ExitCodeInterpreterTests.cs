using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

[Collection(CultureCollection.Name)]
public class ExitCodeInterpreterTests
{
    [Fact]
    public void Code0_NoChange_IsSuccess()
    {
        var r = ExitCodeInterpreter.Interpret(0);
        Assert.True(r.Success);
        Assert.Equal(0, r.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void CodesBelow8_AreSuccess(int code)
    {
        Assert.True(ExitCodeInterpreter.Interpret(code).Success);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(9)]   // 8 (errore) + 1 (copiati)
    [InlineData(16)]
    [InlineData(24)]  // 16 + 8
    public void CodesWithErrorBits_AreFailure(int code)
    {
        Assert.False(ExitCodeInterpreter.Interpret(code).Success);
    }

    [Fact]
    public void Code1_ReportsFilesCopied()
    {
        using var _ = Italian();
        var r = ExitCodeInterpreter.Interpret(1);
        Assert.Contains(r.Details, d => d.Contains("copiat", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Code2_ReportsExtraItems()
    {
        using var _ = Italian();
        var r = ExitCodeInterpreter.Interpret(2);
        Assert.Contains(r.Details, d => d.Contains("extra", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Code8_ReportsErrors()
    {
        using var _ = Italian();
        var r = ExitCodeInterpreter.Interpret(8);
        Assert.False(r.Success);
        Assert.Contains(r.Details, d => d.Contains("error", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Code16_ReportsFatalError()
    {
        using var _ = Italian();
        var r = ExitCodeInterpreter.Interpret(16);
        Assert.False(r.Success);
        Assert.Contains(r.Details, d => d.Contains("grave", StringComparison.OrdinalIgnoreCase));
    }

    // Forza la cultura italiana per le asserzioni sul testo, ripristinandola alla fine.
    private static IDisposable Italian()
    {
        var prev = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("it");
        return new Restore(prev);
    }

    private sealed class Restore : IDisposable
    {
        private readonly System.Globalization.CultureInfo _prev;
        public Restore(System.Globalization.CultureInfo prev) => _prev = prev;
        public void Dispose() => System.Globalization.CultureInfo.CurrentUICulture = _prev;
    }

    [Fact]
    public void NegativeCode_IsFailure()
    {
        Assert.False(ExitCodeInterpreter.Interpret(-1).Success);
    }

    [Fact]
    public void Summary_IsNeverEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(ExitCodeInterpreter.Interpret(0).Summary));
        Assert.False(string.IsNullOrWhiteSpace(ExitCodeInterpreter.Interpret(9).Summary));
    }
}
