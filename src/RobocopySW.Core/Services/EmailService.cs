using System.Net;
using System.Net.Mail;
using System.Text;
using RobocopySW.Core.Models;

namespace RobocopySW.Core.Services;

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
    /// Rispetta <see cref="EmailSettings.OnlyOnError"/>. Restituisce true se l'email è stata inviata.
    /// </summary>
    public async Task<bool> SendResultAsync(EmailSettings settings, JobResult result, string? attachmentPath = null)
    {
        if (!settings.Enabled)
            return false;
        if (settings.OnlyOnError && result.Success)
            return false;

        var esito = result.Success ? "OK" : CoreLoc.S("Lbl_Error");
        var subject = $"[RobocopySW] {esito} - {result.JobName}";

        var yesNo = result.DryRun ? CoreLoc.S("Email_Yes") : CoreLoc.S("Email_No");
        var body = new StringBuilder()
            .AppendLine($"Job: {result.JobName}")
            .AppendLine($"{CoreLoc.S("Lbl_Result")}: {esito} (exit {result.ExitCode}) - {result.Status}")
            .AppendLine($"{CoreLoc.S("Email_Preview")}: {yesNo}")
            .AppendLine($"{CoreLoc.S("Email_Start")}: {result.StartedAt:yyyy-MM-dd HH:mm:ss}  {CoreLoc.S("Lbl_Duration")}: {result.Duration:hh\\:mm\\:ss}")
            .AppendLine()
            .AppendLine($"{CoreLoc.S("Lbl_FoldersCopied"),-16}: {result.DirsCopied}")
            .AppendLine($"{CoreLoc.S("Lbl_FilesCopied"),-16}: {result.FilesCopied}")
            .AppendLine($"{CoreLoc.S("Lbl_FilesUnchanged"),-16}: {result.FilesSkipped}")
            .AppendLine($"{CoreLoc.S("Lbl_FilesExtra"),-16}: {result.FilesExtra}")
            .AppendLine($"{CoreLoc.S("Lbl_FilesFailed"),-16}: {result.FilesFailed}")
            .AppendLine($"{CoreLoc.S("Lbl_DirsFailed"),-16}: {result.DirsFailed}")
            .ToString();

        using var message = new MailMessage(settings.From, settings.To, subject, body);
        if (!string.IsNullOrWhiteSpace(attachmentPath) && File.Exists(attachmentPath))
            message.Attachments.Add(new Attachment(attachmentPath));

        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort) { EnableSsl = settings.UseSsl };
        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            var pwd = _credentials.Unprotect(settings.PasswordProtected ?? "");
            client.Credentials = new NetworkCredential(settings.Username, pwd);
        }

        await client.SendMailAsync(message).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Invia un'email di prova con le impostazioni date (ignora Enabled/OnlyOnError).
    /// Se <paramref name="plainPassword"/> è valorizzata, usa quella invece di quella salvata.
    /// Solleva eccezione in caso di errore, così la UI può mostrare il messaggio SMTP.
    /// </summary>
    public async Task SendTestAsync(EmailSettings settings, string? plainPassword = null)
    {
        var subject = "[RobocopySW] " + CoreLoc.S("Test_Subject");
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
