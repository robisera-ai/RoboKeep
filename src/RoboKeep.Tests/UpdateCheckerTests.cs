using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class UpdateCheckerTests
{
    private const string ReleaseJson = """
    {
      "tag_name": "v1.7.0",
      "html_url": "https://github.com/robisera-ai/RoboKeep/releases/tag/v1.7.0",
      "assets": [
        { "name": "RoboKeep-1.7.0-win-x64-framework-dependent.zip", "size": 3455526,
          "browser_download_url": "https://github.com/robisera-ai/RoboKeep/releases/download/v1.7.0/RoboKeep-1.7.0-win-x64-framework-dependent.zip" },
        { "name": "RoboKeep-1.7.0-win-x64-selfcontained.zip", "size": 68512001,
          "browser_download_url": "https://github.com/robisera-ai/RoboKeep/releases/download/v1.7.0/RoboKeep-1.7.0-win-x64-selfcontained.zip" }
      ]
    }
    """;

    [Fact]
    public void Parse_ReadsVersionPageAndBothAssets()
    {
        var info = UpdateChecker.Parse(ReleaseJson)!;
        Assert.Equal(new Version(1, 7, 0), info.Latest);
        Assert.EndsWith("/tag/v1.7.0", info.ReleaseUrl);
        Assert.Equal(68512001, info.SelfContainedSize);
        Assert.EndsWith("selfcontained.zip", info.SelfContainedUrl);
        Assert.Equal(3455526, info.FrameworkDependentSize);
        Assert.EndsWith("framework-dependent.zip", info.FrameworkDependentUrl);
    }

    [Theory]
    [InlineData("""{ "tag_name": "v1.7.0", "html_url": "x", "assets": [] }""")]           // niente pacchetti
    [InlineData("""{ "tag_name": "nightly", "html_url": "x", "assets": [] }""")]          // tag non parsabile
    [InlineData("""{ "tag_name": "v1.8", "html_url": "x", "assets": [] }""")]             // tag a due componenti
    [InlineData("""{ "message": "Not Found" }""")]                                        // risposta di errore
    [InlineData("non json")]
    public void Parse_ReturnsNull_WhenTheReleaseIsNotUsable(string json)
        => Assert.Null(UpdateChecker.Parse(json));

    [Fact]
    public void Parse_ReturnsNull_WhenAnAssetUrlIsNotTrusted()
    {
        var json = ReleaseJson.Replace(
            "https://github.com/robisera-ai/RoboKeep/releases/download/v1.7.0/RoboKeep-1.7.0-win-x64-selfcontained.zip",
            "http://evil/RoboKeep-1.7.0-win-x64-selfcontained.zip");
        Assert.Null(UpdateChecker.Parse(json));
    }

    [Fact]
    public void Parse_FallsBackToTheReleasesPage_WhenHtmlUrlIsNotTrusted()
    {
        var json = ReleaseJson.Replace(
            "https://github.com/robisera-ai/RoboKeep/releases/tag/v1.7.0", "file:///x");
        var info = UpdateChecker.Parse(json)!;
        Assert.Equal("https://github.com/robisera-ai/RoboKeep/releases/latest", info.ReleaseUrl);
    }

    [Fact]
    public void Parse_IgnoresAssetsThatAreNotThePackages()
    {
        var json = """
        {
          "tag_name": "v1.7.0",
          "html_url": "https://github.com/robisera-ai/RoboKeep/releases/tag/v1.7.0",
          "assets": [
            { "name": "SHA256SUMS.txt",
              "browser_download_url": "https://github.com/robisera-ai/RoboKeep/releases/download/v1.7.0/SHA256SUMS.txt" },
            { "size": 12 },
            { "name": "RoboKeep-1.7.0-win-x64-framework-dependent.zip", "size": 3455526,
              "browser_download_url": "https://github.com/robisera-ai/RoboKeep/releases/download/v1.7.0/RoboKeep-1.7.0-win-x64-framework-dependent.zip" },
            { "name": "RoboKeep-1.7.0-win-x64-selfcontained.zip", "size": 68512001,
              "browser_download_url": "https://github.com/robisera-ai/RoboKeep/releases/download/v1.7.0/RoboKeep-1.7.0-win-x64-selfcontained.zip" }
          ]
        }
        """;
        var info = UpdateChecker.Parse(json)!;
        Assert.Equal(new Version(1, 7, 0), info.Latest);
        Assert.Equal(3455526, info.FrameworkDependentSize);
        Assert.Equal(68512001, info.SelfContainedSize);
    }

    [Fact]
    public void Current_HasThreeComponents()
    {
        Assert.Equal(-1, UpdateChecker.Current.Revision);
        Assert.True(UpdateChecker.Current.Build >= 0);
    }

    [Theory]
    [InlineData("1.8.0", "1.7.0", null, true)]
    [InlineData("1.7.0", "1.7.0", null, false)]
    [InlineData("1.6.9", "1.7.0", null, false)]
    [InlineData("1.8.0", "1.7.0", "1.8.0", false)]   // ignorata
    [InlineData("1.8.1", "1.7.0", "1.8.0", true)]    // ignorata la 1.8.0, la 1.8.1 si propone
    [InlineData("1.8.0", "1.7.0", "abc", true)]      // valore ignorato illeggibile: si propone
    public void IsNewer_ComparesAndHonoursIgnored(string latest, string current, string? ignored, bool expected)
        => Assert.Equal(expected, UpdateChecker.IsNewer(Version.Parse(latest), Version.Parse(current), ignored));

    [Fact]
    public void IsDue_Every24Hours()
    {
        var now = new DateTime(2026, 9, 23, 12, 0, 0);
        Assert.True(UpdateChecker.IsDue(null, now));
        Assert.False(UpdateChecker.IsDue(now.AddHours(-23), now));
        Assert.True(UpdateChecker.IsDue(now.AddHours(-25), now));
    }

    private static (System.Net.HttpListener Listener, string Url) StartServer(byte[] body, int statusCode = 200)
    {
        System.Net.HttpListener? listener = null;
        int port = 0;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            port = new Random().Next(20000, 60000);
            listener = new System.Net.HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            try { listener.Start(); break; }
            catch (System.Net.HttpListenerException)
            {
                // La porta era occupata: chiudere il listener scartato, altrimenti resta appeso.
                listener.Close();
                listener = null;
                if (attempt == 2) throw;
            }
        }
        _ = Task.Run(async () =>
        {
            while (listener!.IsListening)
            {
                System.Net.HttpListenerContext ctx;
                try { ctx = await listener.GetContextAsync(); } catch { break; }
                ctx.Response.StatusCode = statusCode;
                ctx.Response.ContentLength64 = body.Length;
                await ctx.Response.OutputStream.WriteAsync(body);
                ctx.Response.Close();
            }
        });
        return (listener!, $"http://127.0.0.1:{port}/RoboKeep-test.zip");
    }

    [Fact]
    public async Task Download_WritesTheFile_WhenSizeMatches()
    {
        var body = new byte[100_000];
        new Random(1).NextBytes(body);
        var (server, url) = StartServer(body);
        var dir = Path.Combine(Path.GetTempPath(), "RbcDl_" + Guid.NewGuid().ToString("N"));
        var target = Path.Combine(dir, "pacchetto.zip");
        try
        {
            var seen = new SyncProgress();
            var ok = await UpdateChecker.DownloadAsync(url, target, body.Length, seen, CancellationToken.None);
            Assert.True(ok);
            Assert.Equal(body, File.ReadAllBytes(target));
            Assert.False(File.Exists(target + ".part"));
            Assert.Contains(seen.Values, p => p >= 0.99);
        }
        finally { server.Close(); if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Download_LeavesNothing_WhenSizeIsWrong()
    {
        var (server, url) = StartServer(new byte[1000]);
        var dir = Path.Combine(Path.GetTempPath(), "RbcDl_" + Guid.NewGuid().ToString("N"));
        var target = Path.Combine(dir, "pacchetto.zip");
        try
        {
            var ok = await UpdateChecker.DownloadAsync(url, target, expectedSize: 999, null, CancellationToken.None);
            Assert.False(ok);
            Assert.False(File.Exists(target));
            Assert.False(File.Exists(target + ".part"));
        }
        finally { server.Close(); if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Fetch_ReturnsNull_WhenServerIsUnreachable()
        => Assert.Null(await UpdateChecker.FetchAsync(CancellationToken.None, "http://127.0.0.1:9/nessuno"));

    [Fact]
    public async Task FetchWithStatus_DistinguishesNotFound_FromUnreachable()
    {
        // Repository privato o rimosso: GitHub risponde 404. Chi preme "Controlla ora" deve
        // leggere un motivo diverso da "rete assente".
        var (server, url) = StartServer(System.Text.Encoding.UTF8.GetBytes("{\"message\":\"Not Found\"}"), statusCode: 404);
        try
        {
            var (info, status) = await UpdateChecker.FetchWithStatusAsync(CancellationToken.None, url);
            Assert.Null(info);
            Assert.Equal(FetchStatus.NotFound, status);
        }
        finally { server.Close(); }

        var (_, unreachable) = await UpdateChecker.FetchWithStatusAsync(CancellationToken.None, "http://127.0.0.1:9/nessuno");
        Assert.Equal(FetchStatus.Unreachable, unreachable);
    }

    /// <summary>Raccoglie i valori di avanzamento sul thread chiamante: <see cref="Progress{T}"/>
    /// consegna in modo asincrono e costringerebbe il test ad aspettare.</summary>
    private sealed class SyncProgress : IProgress<double>
    {
        private readonly List<double> _values = new();
        public void Report(double value) { lock (_values) _values.Add(value); }
        public IReadOnlyList<double> Values { get { lock (_values) return _values.ToList(); } }
    }
}
