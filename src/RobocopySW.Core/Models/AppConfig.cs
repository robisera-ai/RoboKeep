namespace RobocopySW.Core.Models;

/// <summary>
/// Configurazione completa dell'applicazione, serializzata in <c>config.json</c>:
/// impostazioni globali, credenziali e lista dei job.
/// </summary>
public sealed class AppConfig
{
    public AppSettings Settings { get; set; } = new();
    public List<CredentialEntry> Credentials { get; set; } = new();
    public List<BackupJob> Jobs { get; set; } = new();
}
