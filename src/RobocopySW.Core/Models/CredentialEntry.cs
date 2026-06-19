namespace RobocopySW.Core.Models;

/// <summary>
/// Credenziale per accedere a una share di rete UNC.
/// La password è memorizzata cifrata con DPAPI (mai in chiaro nel file di config).
/// </summary>
public sealed class CredentialEntry
{
    /// <summary>Identificativo referenziato da <see cref="BackupJob.CredentialId"/>.</summary>
    public string Id { get; set; } = "";

    /// <summary>Host o share di destinazione, es. <c>\\server\share</c> oppure <c>server</c>.</summary>
    public string Host { get; set; } = "";

    /// <summary>Nome utente, es. <c>DOMINIO\utente</c>.</summary>
    public string User { get; set; } = "";

    /// <summary>Password protetta con DPAPI (Base64).</summary>
    public string PasswordProtected { get; set; } = "";
}
