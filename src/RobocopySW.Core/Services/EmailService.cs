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

        var esito = result.Success ? "OK" : "ERRORE";
        var subject = $"[RobocopySW] {esito} - {result.JobName}";

        var body = new StringBuilder()
            .AppendLine($"Job: {result.JobName}")
            .AppendLine($"Esito: {esito} (exit code {result.ExitCode}) - {result.Status}")
            .AppendLine($"Anteprima: {(result.DryRun ? "sì" : "no")}")
            .AppendLine($"Inizio: {result.StartedAt:yyyy-MM-dd HH:mm:ss}  Durata: {result.Duration:hh\\:mm\\:ss}")
            .AppendLine()
            .AppendLine($"Cartelle copiate: {result.DirsCopied}")
            .AppendLine($"File copiati:     {result.FilesCopied}")
            .AppendLine($"File ignorati:    {result.FilesSkipped}")
            .AppendLine($"File extra:       {result.FilesExtra}")
            .AppendLine($"File falliti:     {result.FilesFailed}")
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
