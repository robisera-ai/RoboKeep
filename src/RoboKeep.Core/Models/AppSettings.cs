namespace RoboKeep.Core.Models;

/// <summary>Ambito di cifratura DPAPI delle password.</summary>
public enum CredentialProtectionScope
{
    /// <summary>Legata al PC: decifrabile da qualunque utente della macchina (comodo per la schedulazione).</summary>
    Machine,
    /// <summary>Legata all'utente: decifrabile solo dall'utente Windows che l'ha salvata (più sicuro).</summary>
    User,
}

/// <summary>Impostazioni globali dell'applicazione (non legate al singolo job).</summary>
public sealed class AppSettings
{
    /// <summary>Ambito di cifratura DPAPI per le password (credenziali ed email).</summary>
    public CredentialProtectionScope CredentialScope { get; set; } = CredentialProtectionScope.Machine;

    /// <summary>Lingua dell'interfaccia: "it", "en" oppure null/"auto" = segue Windows.</summary>
    public string? Language { get; set; }

    /// <summary>Cartella radice dei log archiviati (sotto vengono creati i giorni AAAAMMGG).</summary>
    public string LogRoot { get; set; } = "";

    /// <summary>Cartella temporanea per i log prima dell'eventuale compressione.</summary>
    public string TempRoot { get; set; } = "";

    /// <summary>Se true i log vengono compressi in .zip dopo l'esecuzione.</summary>
    public bool CompressLogs { get; set; } = true;

    /// <summary>Giorni di conservazione dei log archiviati; oltre vengono eliminati. 0 = nessuna pulizia.</summary>
    public int LogRetentionDays { get; set; } = 30;

    /// <summary>Giorni dopo i quali un backup riuscito è considerato "fermo". 0 = disattiva l'allerta "fermo".</summary>
    public int StaleAfterDays { get; set; } = 7;

    /// <summary>Se true, prima di avviare un job esegue i controlli pre-avvio (raggiungibilità, spazio).</summary>
    public bool PreflightEnabled { get; set; } = true;

    /// <summary>Soglia minima di spazio libero in destinazione (MB) sotto la quale il pre-check avvisa.</summary>
    public int MinFreeSpaceMb { get; set; } = 1024;

    /// <summary>Se true mostra notifiche toast a fine job.</summary>
    public bool NotificationsEnabled { get; set; } = true;

    /// <summary>Se true la chiusura/riduzione manda l'app nell'area di notifica (tray) invece di chiudere.</summary>
    public bool MinimizeToTray { get; set; } = false;

    /// <summary>Se true l'app si avvia minimizzata nel tray.</summary>
    public bool StartMinimized { get; set; } = false;

    /// <summary>Controllo aggiornamenti automatico: null = mai chiesto (la finestra principale lo
    /// chiede una volta), true/false = scelta dell'utente. Con false l'app non fa nessuna
    /// connessione da se'; il pulsante "Controlla ora" delle Impostazioni resta valido perche'
    /// e' l'utente a chiederlo in quel momento. Data dell'ultimo controllo e versione ignorata
    /// stanno in <c>update-state.json</c> (vedi <c>UpdateStateStore</c>), non qui: si scrivono
    /// anche a finestra Impostazioni aperta, dove la configurazione non va salvata.</summary>
    public bool? UpdateCheck { get; set; }

    /// <summary>Se true, dopo ogni backup riuscito RoboKeep scrive una copia della configurazione
    /// nella radice del disco di destinazione (<c>&lt;radice&gt;\RoboKeep-config\</c>, vedi
    /// <c>ConfigMirror</c>). Un disco che ha i file ma non i job costringe a ricostruire tutto a
    /// memoria: e' la seconda meta' del backup, e per questo e' attiva di default. Le destinazioni
    /// di rete sono escluse; le password restano cifrate DPAPI.</summary>
    public bool ConfigCopyToDestination { get; set; } = true;

    /// <summary>Impostazioni di notifica email.</summary>
    public EmailSettings Email { get; set; } = new();
}

/// <summary>Impostazioni SMTP per le notifiche email di esito.</summary>
public sealed class EmailSettings
{
    public bool Enabled { get; set; }
    public string SmtpHost { get; set; } = "";

    /// <summary>Porta SMTP: default 587, porta submission standard con STARTTLS.</summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>Se true usa una connessione cifrata (STARTTLS/SSL): attivo di default per non
    /// spedire credenziali ed esiti backup in chiaro sulla rete.</summary>
    public bool UseSsl { get; set; } = true;
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string? Username { get; set; }

    /// <summary>Password SMTP protetta con DPAPI (Base64). Mai in chiaro.</summary>
    public string? PasswordProtected { get; set; }

    /// <summary>Se true invia l'email solo quando il job termina in errore.</summary>
    public bool OnlyOnError { get; set; } = true;
}
