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

        var esito = result.Success ? "OK" : CoreLoc.S("ERRORE", "ERROR");
        var subject = $"[RobocopySW] {esito} - {result.JobName}";

        var yesNo = result.DryRun ? CoreLoc.S("sì", "yes") : CoreLoc.S("no", "no");
        var body = new StringBuilder()
            .AppendLine($"Job: {result.JobName}")
            .AppendLine($"{CoreLoc.S("Esito", "Result")}: {esito} (exit {result.ExitCode}) - {result.Status}")
            .AppendLine($"{CoreLoc.S("Anteprima", "Preview")}: {yesNo}")
            .AppendLine($"{CoreLoc.S("Inizio", "Start")}: {result.StartedAt:yyyy-MM-dd HH:mm:ss}  {CoreLoc.S("Durata", "Duration")}: {result.Duration:hh\\:mm\\:ss}")
            .AppendLine()
            .AppendLine($"{CoreLoc.S("Cartelle copiate", "Folders copied"),-16}: {result.DirsCopied}")
            .AppendLine($"{CoreLoc.S("File copiati", "Files copied"),-16}: {result.FilesCopied}")
            .AppendLine($"{CoreLoc.S("File invariati", "Files unchanged"),-16}: {result.FilesSkipped}")
            .AppendLine($"{CoreLoc.S("File extra", "Extra files"),-16}: {result.FilesExtra}")
            .AppendLine($"{CoreLoc.S("File falliti", "Files failed"),-16}: {result.FilesFailed}")
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
}
