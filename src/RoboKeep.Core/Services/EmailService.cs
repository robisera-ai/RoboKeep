using System.Net;
using System.Net.Mail;
using System.Text;
using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>
/// Invia notifiche email con l'esito di un job via SMTP.
/// Sostituisce lo script storico <c>vbs/sendmail.vbs</c>.
/// </summary>
public sealed class EmailService
{
    private readonly CredentialService _credentials;

    public EmailService(CredentialService credentials) => _credentials = credentials;

    /// <summary>
    /// Invia l'email di esito se le impostazioni lo prevedono.
    /// Rispetta <see cref="EmailSettings.OnlyOnError"/>, ma un run riuscito con avvisi di salute
    /// del disco viene comunque notificato: è il preavviso che conta.
    /// Restituisce true se l'email è stata inviata.
    /// </summary>
    public async Task<bool> SendResultAsync(EmailSettings settings, JobResult result, string? attachmentPath = null, CancellationToken ct = default)
    {
        if (!ShouldSend(settings, result))
            return false;

        var subject = BuildSubject(result);
        var body = BuildBody(result);

        using var message = new MailMessage(settings.From, settings.To, subject, body);
        if (!string.IsNullOrWhiteSpace(attachmentPath) && File.Exists(attachmentPath))
            message.Attachments.Add(new Attachment(attachmentPath));

        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
        {
            EnableSsl = settings.UseSsl,
            Timeout = 20000, // 20s: un SMTP che non risponde non blocca il job
        };
        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            var pwd = _credentials.Unprotect(settings.PasswordProtected ?? "");
            client.Credentials = new NetworkCredential(settings.Username, pwd);
        }

        await client.SendMailAsync(message, ct).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Decide se l'email va inviata: rispetta <see cref="EmailSettings.Enabled"/> e
    /// <see cref="EmailSettings.OnlyOnError"/>, ma un successo con avvisi di salute del disco
    /// passa comunque, perché è già il preavviso che conta — tranne in anteprima: un'anteprima
    /// riuscita non ha notizie da dare (gli stessi avvisi li ha già mandati il run vero) e chi
    /// prova i filtri di un job non deve riempirsi la casella.
    /// </summary>
    public static bool ShouldSend(EmailSettings settings, JobResult result)
    {
        if (!settings.Enabled)
            return false;
        if (settings.OnlyOnError && result.Success && (result.DryRun || result.HealthWarnings.Count == 0))
            return false;
        return true;
    }

    private static string Esito(JobResult r) =>
        r.HardwareError ? CoreLoc.S("Email_HardwareError") : r.Success ? "OK" : CoreLoc.S("Lbl_Error");

    /// <summary>Oggetto: un errore HARDWARE va riconosciuto già dall'anteprima sul telefono.</summary>
    public static string BuildSubject(JobResult result) => $"[RoboKeep] {Esito(result)} - {result.JobName}";

    /// <summary>Corpo: per un errore hardware prima cosa è successo e cosa fare, poi i conteggi;
    /// in coda gli avvisi di salute del disco.</summary>
    public static string BuildBody(JobResult result)
    {
        var esito = Esito(result);
        var yesNo = result.DryRun ? CoreLoc.S("Email_Yes") : CoreLoc.S("Email_No");
        var sb = new StringBuilder();
        if (result.HardwareError)
        {
            // Prima di tutto: cosa è successo e cosa fare. I conteggi qui non interessano a nessuno.
            // Un job che non è nemmeno partito non va annunciato come "INTERROTTO": il suo dettaglio
            // è già la frase completa ("job NON eseguito: il disco … è a riposo"), si usa così com'è.
            sb.AppendLine(result.NotStarted
                  ? result.HardwareErrorDetail ?? ""
                  : string.Format(CoreLoc.S("Hw_Stop"), result.HardwareErrorDetail))
              .AppendLine(CoreLoc.S("Hw_Advice"))
              .AppendLine();
        }
        sb.AppendLine($"Job: {result.JobName}")
          .AppendLine($"{CoreLoc.S("Lbl_Result")}: {esito} (exit {result.ExitCode}) - {result.Status}")
          .AppendLine($"{CoreLoc.S("Email_Preview")}: {yesNo}")
          .AppendLine($"{CoreLoc.S("Email_Start")}: {result.StartedAt:yyyy-MM-dd HH:mm:ss}  {CoreLoc.S("Lbl_Duration")}: {result.Duration:hh\\:mm\\:ss}")
          .AppendLine()
          .AppendLine($"{CoreLoc.S("Lbl_FoldersCopied"),-16}: {result.DirsCopied}")
          .AppendLine($"{CoreLoc.S("Lbl_FilesCopied"),-16}: {result.FilesCopied}")
          .AppendLine($"{CoreLoc.S("Lbl_FilesUnchanged"),-16}: {result.FilesSkipped}")
          .AppendLine($"{CoreLoc.S("Lbl_FilesExtra"),-16}: {result.FilesExtra}")
          .AppendLine($"{CoreLoc.S("Lbl_FilesFailed"),-16}: {result.FilesFailed}")
          .AppendLine($"{CoreLoc.S("Lbl_DirsFailed"),-16}: {result.DirsFailed}");
        if (result.HealthWarnings.Count > 0)
        {
            sb.AppendLine();
            foreach (var w in result.HealthWarnings) sb.AppendLine(w);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Invia un'email di prova con le impostazioni date (ignora Enabled/OnlyOnError).
    /// Se <paramref name="plainPassword"/> è valorizzata, usa quella invece di quella salvata.
    /// Solleva eccezione in caso di errore, così la UI può mostrare il messaggio SMTP.
    /// </summary>
    public async Task SendTestAsync(EmailSettings settings, string? plainPassword = null)
    {
        var subject = "[RoboKeep] " + CoreLoc.S("Test_Subject");
        using var message = new MailMessage(settings.From, settings.To, subject, CoreLoc.S("Test_Body"));

        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
        {
            EnableSsl = settings.UseSsl,
            Timeout = 20000, // 20s: una config errata fallisce in fretta
        };
        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            var pwd = !string.IsNullOrEmpty(plainPassword)
                ? plainPassword
                : _credentials.Unprotect(settings.PasswordProtected ?? "");
            client.Credentials = new NetworkCredential(settings.Username, pwd);
        }

        await client.SendMailAsync(message).ConfigureAwait(false);
    }
}
