using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace RoboKeep.Core.Services;

/// <summary>Esito di una richiesta a GitHub.</summary>
public enum FetchStatus
{
    /// <summary>Release letta e usabile.</summary>
    Ok,
    /// <summary>404: repository o release non pubblici (o rimossi).</summary>
    NotFound,
    /// <summary>Rete assente, timeout, altro errore HTTP.</summary>
    Unreachable,
    /// <summary>Risposta ricevuta ma non e' una release usabile (tag strano, pacchetti mancanti).</summary>
    Invalid,
}

/// <summary>Ultima release pubblicata su GitHub, ridotta a cio' che serve all'app.</summary>
public sealed record UpdateInfo(
    Version Latest, string ReleaseUrl,
    string SelfContainedUrl, long SelfContainedSize,
    string FrameworkDependentUrl, long FrameworkDependentSize);

/// <summary>
/// Controllo aggiornamenti: una richiesta HTTPS a GitHub (releases/latest), nessun dato
/// dell'utente oltre a IP e User-Agent. Parte pura testabile (parse, confronto, cadenza) e
/// parte di rete best-effort: qualunque errore = "nessuna informazione", mai un'eccezione a video.
/// </summary>
public static class UpdateChecker
{
    public const string LatestReleaseApi = "https://api.github.com/repos/robisera-ai/RoboKeep/releases/latest";
    /// <summary>Pagina da aprire quando la risposta non porta un indirizzo attendibile.</summary>
    public const string ReleasesPage = "https://github.com/robisera-ai/RoboKeep/releases/latest";
    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Legge la risposta dell'API; null se non e' una release usabile (tag non
    /// parsabile, manca uno dei due pacchetti, JSON di errore o rotto).</summary>
    public static UpdateInfo? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("tag_name", out var tagEl) || !root.TryGetProperty("assets", out var assets)) return null;
            var tag = (tagEl.GetString() ?? "").TrimStart('v', 'V');
            if (!Version.TryParse(tag, out var version) || version.Build < 0) return null;
            var url = root.TryGetProperty("html_url", out var u) ? u.GetString() ?? "" : "";
            if (!IsTrustedUrl(url)) url = ReleasesPage;

            string? scUrl = null, fdUrl = null; long scSize = 0, fdSize = 0;
            foreach (var a in assets.EnumerateArray())
            {
                // Una release puo' portare allegati che non ci riguardano (checksum, note) e
                // campi mancanti: si saltano, non invalidano la release.
                if (a.ValueKind != JsonValueKind.Object) continue;
                if (!a.TryGetProperty("name", out var nameEl) || nameEl.ValueKind != JsonValueKind.String) continue;
                if (!a.TryGetProperty("browser_download_url", out var dlEl) || dlEl.ValueKind != JsonValueKind.String) continue;
                if (!a.TryGetProperty("size", out var sizeEl) || !sizeEl.TryGetInt64(out var size)) continue;

                var name = nameEl.GetString() ?? "";
                var dl = dlEl.GetString() ?? "";
                var isSelfContained = name.Equals(InstallKind.AssetName(version, true), StringComparison.OrdinalIgnoreCase);
                var isFrameworkDependent = name.Equals(InstallKind.AssetName(version, false), StringComparison.OrdinalIgnoreCase);
                if (!isSelfContained && !isFrameworkDependent) continue;
                // Pacchetto con indirizzo non attendibile: vale come mancante (la release
                // verra' scartata), mai come qualcosa da scaricare.
                if (!IsTrustedUrl(dl)) continue;

                if (isSelfContained) { scUrl = dl; scSize = size; }
                else { fdUrl = dl; fdSize = size; }
            }
            if (scUrl is null || fdUrl is null) return null;
            return new UpdateInfo(version, url, scUrl, scSize, fdUrl, fdSize);
        }
        catch { return null; }
    }

    /// <summary>Indirizzo accettabile per aprirlo o scaricarlo: https assoluto e ospitato da
    /// GitHub. La risposta arriva dalla rete: senza questo controllo un JSON manomesso potrebbe
    /// far aprire o scaricare qualunque cosa.</summary>
    private static bool IsTrustedUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps) return false;
        var host = uri.Host;
        return host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>true se la release e' piu' recente di quella in uso e non e' quella ignorata.</summary>
    public static bool IsNewer(Version latest, Version current, string? ignored)
    {
        if (latest <= current) return false;
        return !(Version.TryParse(ignored ?? "", out var ign) && ign == latest);
    }

    public static bool IsDue(DateTime? lastCheck, DateTime now) =>
        lastCheck is null || now - lastCheck.Value >= CheckInterval;

    /// <summary>Versione dell'app in uso (Major.Minor.Build).</summary>
    public static Version Current
    {
        get
        {
            var v = typeof(UpdateChecker).Assembly.GetName().Version ?? new Version(0, 0, 0);
            return new Version(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build);
        }
    }

    private static HttpClient NewClient()
    {
        var c = new HttpClient { Timeout = FetchTimeout };
        // GitHub rifiuta le richieste senza User-Agent.
        c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RoboKeep", Current.ToString()));
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return c;
    }

    /// <summary>Interroga GitHub; null su qualunque errore (rete assente, timeout, 404, JSON inatteso).</summary>
    public static async Task<UpdateInfo?> FetchAsync(CancellationToken ct = default, string? apiUrl = null)
        => (await FetchWithStatusAsync(ct, apiUrl).ConfigureAwait(false)).Info;

    /// <summary>Come <see cref="FetchAsync"/>, ma dice anche perche' non c'e' un risultato:
    /// <see cref="FetchStatus.NotFound"/> (404: release o repository non pubblici) e' una
    /// situazione diversa da "rete assente", e chi preme "Controlla ora" deve poterle distinguere.</summary>
    public static async Task<(UpdateInfo? Info, FetchStatus Status)> FetchWithStatusAsync(
        CancellationToken ct = default, string? apiUrl = null)
    {
        try
        {
            using var client = NewClient();
            using var response = await client.GetAsync(apiUrl ?? LatestReleaseApi, ct).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return (null, FetchStatus.NotFound);
            if (!response.IsSuccessStatusCode) return (null, FetchStatus.Unreachable);
            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var info = Parse(json);
            return info is null ? (null, FetchStatus.Invalid) : (info, FetchStatus.Ok);
        }
        catch { return (null, FetchStatus.Unreachable); }
    }

    /// <summary>Scarica in <paramref name="targetPath"/> passando da un file .part; verifica la
    /// dimensione attesa (se > 0) e rinomina. Restituisce false (e non lascia file) su errore o
    /// dimensione sbagliata. Lancia solo OperationCanceledException.</summary>
    public static async Task<bool> DownloadAsync(string url, string targetPath, long expectedSize,
        IProgress<double>? progress, CancellationToken ct)
    {
        var part = targetPath + ".part";
        try
        {
            using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RoboKeep", Current.ToString()));
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? expectedSize;

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            long done = 0;
            var lastPercent = -1;
            await using (var input = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            await using (var output = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
            {
                var buffer = new byte[1 << 16];
                int n;
                while ((n = await input.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                    done += n;
                    // Piu' byte del previsto: la risposta non e' il pacchetto che ci aspettavamo.
                    // Fermarsi subito evita di riempire il disco con un flusso senza fine; il
                    // controllo di dimensione qui sotto cancella il .part e restituisce false.
                    if (expectedSize > 0 && done > expectedSize) break;
                    if (total <= 0) continue;
                    // Un aggiornamento per punto percentuale: il banner non deve essere riscritto
                    // a ogni blocco letto.
                    var percent = (int)(done * 100 / total);
                    if (percent == lastPercent) continue;
                    lastPercent = percent;
                    progress?.Report((double)done / total);
                }
            }

            if (expectedSize > 0 && new FileInfo(part).Length != expectedSize) { File.Delete(part); return false; }
            File.Move(part, targetPath, overwrite: true);
            return true;
        }
        catch (OperationCanceledException) { TryDelete(part); throw; }
        catch { TryDelete(part); return false; }
    }

    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}
