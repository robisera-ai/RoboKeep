namespace RobocopySW.Core.Models;

/// <summary>Impostazioni globali dell'applicazione (non legate al singolo job).</summary>
public sealed class AppSettings
{
    /// <summary>Cartella radice dei log archiviati (sotto vengono creati i giorni AAAAMMGG).</summary>
    public string LogRoot { get; set; } = "";

    /// <summary>Cartella temporanea per i log prima dell'eventuale compressione.</summary>
    public string TempRoot { get; set; } = "";

    /// <summary>Se true i log vengono compressi in .zip dopo l'esecuzione.</summary>
    public bool CompressLogs { get; set; } = true;

    /// <summary>Giorni di conservazione dei log archiviati; oltre vengono eliminati. 0 = nessuna pulizia.</summary>
    public int LogRetentionDays { get; set; } = 30;

    /// <summary>Impostazioni di notifica email.</summary>
    public EmailSettings Email { get; set; } = new();
}

/// <summary>Impostazioni SMTP per le notifiche email di esito.</summary>
public sealed class EmailSettings
{
    public bool Enabled { get; set; }
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 25;
    public bool UseSsl { get; set; }
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string? Username { get; set; }

    /// <summary>Password SMTP protetta con DPAPI (Base64). Mai in chiaro.</summary>
    public string? PasswordProtected { get; set; }

    /// <summary>Se true invia l'email solo quando il job termina in errore.</summary>
    public bool OnlyOnError { get; set; } = true;
}
