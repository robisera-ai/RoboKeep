using System.Security.Cryptography;

namespace RoboKeep.Core.Services;

/// <summary>Esito della verifica integrità.</summary>
public sealed record VerifyResult(
    long Checked,
    long Mismatched,
    long ChangedSinceBackup,
    long Missing,
    long Skipped,
    IReadOnlyList<string> MismatchedPaths);

/// <summary>
/// Verifica integrità: confronto SHA-256 file per file tra sorgente e destinazione.
/// Anti-falso-allarme: se gli hash differiscono ma la sorgente è stata modificata DOPO la
/// copia (data più recente), il file conta come "modificato dopo il backup", non come
/// corruzione — solo differenze a parità di data sono Mismatched. File illeggibili (in uso)
/// contano come Skipped. Lavoro IO pesante: eseguire da thread di background.
/// </summary>
public static class IntegrityVerifier
{
    private const int MaxMismatchedPaths = 50;
    private const int ProgressEvery = 50;

    public static async Task<VerifyResult> VerifyAsync(
        string sourceDir, string destDir, IProgress<string>? progress, CancellationToken ct)
    {
        return await Task.Run(() => Verify(sourceDir, destDir, progress, ct), ct).ConfigureAwait(false);
    }

    private static VerifyResult Verify(
        string sourceDir, string destDir, IProgress<string>? progress, CancellationToken ct)
    {
        // Salta i reparse point (junction/symlink) come fa robocopy con /XJ: una junction
        // circolare nella sorgente manderebbe in loop l'enumerazione.
        var files = Directory.EnumerateFiles(sourceDir, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        }).ToList();
        long checkedCount = 0, mismatched = 0, changed = 0, missing = 0, skipped = 0;
        var mismatchedPaths = new List<string>();
        var done = 0;

        foreach (var srcFile in files)
        {
            ct.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(sourceDir, srcFile);
            var dstFile = Path.Combine(destDir, relative);
            done++;

            if (done % ProgressEvery == 0)
                progress?.Report(string.Format(CoreLoc.S("Verify_Progress"), done, files.Count));

            if (!File.Exists(dstFile))
            {
                missing++;
                continue;
            }

            byte[] srcHash, dstHash;
            try
            {
                srcHash = HashFile(srcFile, ct);
                dstHash = HashFile(dstFile, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (IOException) { skipped++; continue; }
            catch (UnauthorizedAccessException) { skipped++; continue; }

            checkedCount++;
            if (srcHash.AsSpan().SequenceEqual(dstHash))
                continue;

            // Hash diversi: corruzione o solo sorgente cambiata dopo il backup?
            var srcTime = File.GetLastWriteTimeUtc(srcFile);
            var dstTime = File.GetLastWriteTimeUtc(dstFile);
            if (srcTime > dstTime.AddSeconds(2)) // tolleranza granularità filesystem
            {
                changed++;
            }
            else
            {
                mismatched++;
                if (mismatchedPaths.Count < MaxMismatchedPaths)
                    mismatchedPaths.Add(relative);
            }
        }

        return new VerifyResult(checkedCount, mismatched, changed, missing, skipped, mismatchedPaths);
    }

    private static byte[] HashFile(string path, CancellationToken ct)
    {
        // Hash a blocchi con controllo di annullamento: su un singolo file multi-GB
        // l'annullamento risponde entro un blocco, non a fine file.
        using var sha = SHA256.Create();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1024 * 1024);
        var buffer = new byte[1024 * 1024];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            sha.TransformBlock(buffer, 0, read, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return sha.Hash!;
    }
}
