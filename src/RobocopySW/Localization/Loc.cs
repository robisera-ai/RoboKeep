using System.ComponentModel;
using System.Globalization;

namespace RobocopySW.Localization;

/// <summary>
/// Provider di localizzazione runtime (IT/EN). Espone le stringhe tramite indicizzatore
/// e notifica <c>Item[]</c> al cambio lingua, così i binding XAML si aggiornano in tempo reale.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private Dictionary<string, string> _cur;

    private Loc()
    {
        // Lingua iniziale dedotta da Windows: italiano se il sistema è italiano, altrimenti inglese.
        Language = DetectSystemLanguage();
        _cur = Language == "it" ? It : En;
    }

    /// <summary>Codice lingua corrente: "it" o "en".</summary>
    public string Language { get; private set; }

    public bool IsItalian => Language == "it";

    /// <summary>Stringa localizzata per la chiave; se mancante restituisce la chiave stessa.</summary>
    public string this[string key] => _cur.TryGetValue(key, out var v) ? v : key;

    /// <summary>Comodo per il codice: stringa localizzata della chiave.</summary>
    public string T(string key) => this[key];

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Imposta esplicitamente la lingua ("it" o "en") e aggiorna i binding.</summary>
    public void SetLanguage(string lang)
    {
        lang = lang == "it" ? "it" : "en";
        if (lang == Language) return;
        Language = lang;
        _cur = lang == "it" ? It : En;
        ApplyCulture(lang);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
    }

    /// <summary>Applica l'impostazione salvata: "it"/"en" forzano la lingua, altro = auto da Windows.</summary>
    public void ApplyFromSetting(string? setting)
    {
        var lang = setting switch
        {
            "it" => "it",
            "en" => "en",
            _ => DetectSystemLanguage(),
        };
        Language = lang;
        _cur = lang == "it" ? It : En;
        ApplyCulture(lang);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
    }

    private static string DetectSystemLanguage() =>
        CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "it" ? "it" : "en";

    private static void ApplyCulture(string lang)
    {
        var ci = new CultureInfo(lang);
        CultureInfo.CurrentUICulture = ci;
        CultureInfo.DefaultThreadCurrentUICulture = ci; // così anche Core (recap/esiti) usa la lingua giusta
    }

    // ----- Dizionari -----

    private static readonly Dictionary<string, string> It = new()
    {
        ["Common_Browse"] = "Sfoglia…",
        ["Common_Save"] = "Salva",
        ["Common_Cancel"] = "Annulla",
        ["Common_MissingData"] = "Dati mancanti",
        ["Common_Confirm"] = "Conferma",

        ["Main_Title"] = "RobocopySW — Backup configurabile",
        ["Main_New"] = "Nuovo",
        ["Main_Edit"] = "Modifica",
        ["Main_Delete"] = "Elimina",
        ["Main_MoveUpTip"] = "Sposta su (priorità più alta)",
        ["Main_MoveDownTip"] = "Sposta giù (priorità più bassa)",
        ["Main_Preview"] = "Anteprima",
        ["Main_RunSelected"] = "Avvia selezionato",
        ["Main_RunAll"] = "Avvia tutti",
        ["Main_Settings"] = "Impostazioni",
        ["Main_LogTitle"] = "Log esecuzione",
        ["Main_Clear"] = "Pulisci",

        ["Col_Active"] = "Attivo",
        ["Col_Name"] = "Nome",
        ["Col_Mode"] = "Modalità",
        ["Col_Source"] = "Sorgente",
        ["Col_Dest"] = "Destinazione",
        ["Col_LastResult"] = "Ultimo esito",

        ["Empty_Title"] = "Nessun job configurato",
        ["Empty_Desc"] = "Crea il primo backup: scegli una cartella sorgente e una destinazione.",
        ["Empty_Button"] = "Crea il primo job",

        ["Status_Running"] = "Esecuzione in corso…",
        ["Status_JobsConfigured"] = "{0} job configurati",

        ["Mode_Mirror"] = "Mirror",
        ["Mode_CopyOnly"] = "Solo copia",

        ["Run_Preview"] = "ANTEPRIMA",
        ["Run_Execution"] = "ESECUZIONE",
        ["Run_PreviewStatus"] = "anteprima…",
        ["Run_InProgress"] = "in corso…",
        ["Run_OK"] = "OK",
        ["Run_Error"] = "ERRORE",
        ["Run_Cancelled"] = "annullato",
        ["Run_CancelledUser"] = "annullato dall'utente",
        ["Stat_Copied"] = "copiati",
        ["Stat_Unchanged"] = "invariati",
        ["Stat_Extra"] = "extra",
        ["Stat_Errors"] = "errori",

        ["Delete_Confirm"] = "Eliminare il job '{0}'?",

        ["Editor_TitleEdit"] = "Modifica job",
        ["Editor_NewJobName"] = "Nuovo job",
        ["Editor_Name"] = "Nome",
        ["Editor_NamePlaceholder"] = "es. Documenti",
        ["Editor_Source"] = "Sorgente",
        ["Editor_Dest"] = "Destinazione",
        ["Editor_Mirror"] = "Mirror — rende la destinazione identica alla sorgente (CANCELLA i file rimossi)",
        ["Editor_MirrorHint"] = "Disattivato = copia e aggiorna soltanto, senza mai cancellare in destinazione.",
        ["Editor_ExcludeOlder"] = "Non sovrascrivere i file più recenti in destinazione (/XO)",
        ["Editor_CopyAll"] = "Copia anche ACL/owner (/COPYALL — utile su share di rete)",
        ["Editor_BigJ"] = "Ottimizza file grandi (/J — I/O non bufferizzato)",
        ["Editor_BigJTip"] = "Per file molto grandi (multi-GB): bypassa la cache del file system, spesso più veloce su SSD/NVMe e reti veloci. Non aiuta i file piccoli. Si esclude a vicenda con la copia riavviabile.",
        ["Editor_RestartZ"] = "Copia riavviabile (/Z — riprende se interrotta)",
        ["Editor_RestartZTip"] = "Riprende la copia di un file grande se si interrompe (utile su VPN, Wi-Fi o reti inaffidabili). Ha un po' di overhead: su reti stabili è meglio lasciarla spenta. Si esclude a vicenda con l'ottimizzazione file grandi.",
        ["Editor_Threads"] = "Thread (/MT)",
        ["Editor_ThreadsTip"] = "Numero di file copiati in parallelo (multi-thread). Più alto = più veloce, soprattutto con tanti file piccoli. 8 va bene quasi sempre; 16+ su SSD o share di rete veloci; su hard disk meccanici meglio non esagerare (può far sbattere la testina). 0 disattiva il multi-thread.",
        ["Editor_Retries"] = "Tentativi (/R)",
        ["Editor_RetriesTip"] = "Quante volte riprovare a copiare un file se la copia fallisce (es. file temporaneamente in uso o rete instabile). Con file spesso aperti tieni un valore basso (1) per non aspettare a lungo.",
        ["Editor_Wait"] = "Attesa sec (/W)",
        ["Editor_WaitTip"] = "Secondi di attesa tra un tentativo e l'altro. Si combina con i Tentativi (es. 1 tentativo × 5 sec). Valori alti allungano molto i tempi se ci sono file problematici.",
        ["Editor_Credential"] = "Credenziale di rete (per share UNC)",
        ["Editor_CredentialTip"] = "Serve solo se sorgente o destinazione è una cartella di rete condivisa (\\\\server\\condivisione) che richiede utente e password. Le credenziali si creano in Impostazioni → Credenziali. Per dischi locali o USB lascia «nessuna».",
        ["Editor_ExcludeFiles"] = "Escludi file (uno per riga)",
        ["Editor_ExcludeFilesTip"] = "Schemi di file da NON copiare, uno per riga. Esempi: *.tmp (tutti i .tmp), ~$* (temporanei di Office), *.log. Lascia vuoto per copiare tutto.",
        ["Editor_ExcludeDirs"] = "Escludi cartelle (una per riga)",
        ["Editor_ExcludeDirsTip"] = "Nomi di cartelle da saltare, una per riga. Esempi: cache, node_modules, Temp. Vengono escluse ovunque compaiano nell'albero.",
        ["Editor_CommandPreview"] = "Anteprima comando robocopy",
        ["Editor_Val_Name"] = "Il nome del job è obbligatorio.",
        ["Editor_Val_Source"] = "La cartella sorgente è obbligatoria.",
        ["Editor_Val_Dest"] = "La cartella destinazione è obbligatoria.",
        ["Editor_BrowseTitle"] = "Seleziona cartella",
        ["Cred_NoneLocal"] = "(nessuna — percorso locale)",

        ["Settings_Title"] = "Impostazioni",
        ["Tab_General"] = "Generale",
        ["Tab_Email"] = "Email",
        ["Tab_Credentials"] = "Credenziali di rete",
        ["Tab_Schedule"] = "Pianificazione",

        ["Set_Language"] = "Lingua",
        ["Lang_Auto"] = "Automatica (Windows)",
        ["Set_LogFolder"] = "Cartella log (archivio per data)",
        ["Set_TempFolder"] = "Cartella temporanea",
        ["Set_PathsHint"] = "I log sono SEMPRE attivi. Se lasci vuoti questi campi, log e file temporanei vanno nelle sottocartelle predefinite mostrate in grigio (accanto all'app). Compila un percorso solo per spostarli altrove.",
        ["Set_Compress"] = "Comprimi i log in .zip",
        ["Set_Retention"] = "Giorni di conservazione log (0 = non eliminare)",
        ["Set_CredSecurity"] = "Sicurezza credenziali",
        ["Set_CredScopeUser"] = "Cifra le password solo per il mio utente Windows (più sicuro)",
        ["Set_CredScopeHint"] = "Attivo: password (credenziali ed email) decifrabili solo dal tuo utente; in compenso l'esecuzione pianificata deve girare con il tuo stesso utente. Disattivo: legate al PC, funzionano con qualsiasi utente (comodo per la schedulazione). Cambiando l'opzione, le password già salvate vengono ri-cifrate in automatico.",

        ["Email_Enable"] = "Abilita notifiche email",
        ["Email_OnlyError"] = "Invia solo in caso di errore",
        ["Email_Smtp"] = "Server SMTP",
        ["Email_Port"] = "Porta",
        ["Email_Ssl"] = "Usa SSL/TLS",
        ["Email_From"] = "Da (mittente)",
        ["Email_To"] = "A (destinatario)",
        ["Email_User"] = "Utente SMTP (facoltativo)",
        ["Email_Pwd"] = "Password SMTP (lascia vuoto per non modificare)",

        ["Cred_AddUpdate"] = "Aggiungi / aggiorna credenziale",
        ["Cred_Name"] = "Nome",
        ["Cred_Host"] = "Host/Share (\\\\server\\share)",
        ["Cred_User"] = "Utente (DOMINIO\\utente)",
        ["Cred_Pwd"] = "Password",
        ["Cred_Save"] = "Salva credenziale",
        ["Cred_Remove"] = "Rimuovi selezionata",
        ["Cred_ColUser"] = "Utente",
        ["Cred_NameRequired"] = "Il nome della credenziale è obbligatorio.",

        ["Sched_InfoTitle"] = "Esecuzione automatica",
        ["Sched_InfoMsg"] = "Registra un'attività in Utilità di pianificazione di Windows che esegue «Avvia tutti» in modalità silenziosa all'orario scelto.",
        ["Sched_Time"] = "Orario (HH:mm)",
        ["Sched_Freq"] = "Frequenza",
        ["Sched_Daily"] = "Giornaliera",
        ["Sched_Weekly"] = "Settimanale",
        ["Sched_Create"] = "Crea / aggiorna attività",
        ["Sched_Remove"] = "Rimuovi attività",
        ["Sched_BadTime"] = "Orario non valido. Usa il formato HH:mm.",
        ["Sched_Created"] = "Attività pianificata creata/aggiornata per le {0}.",
        ["Sched_Removed"] = "Attività pianificata rimossa.",
        ["Sched_Error"] = "Errore: {0}",
    };

    private static readonly Dictionary<string, string> En = new()
    {
        ["Common_Browse"] = "Browse…",
        ["Common_Save"] = "Save",
        ["Common_Cancel"] = "Cancel",
        ["Common_MissingData"] = "Missing data",
        ["Common_Confirm"] = "Confirm",

        ["Main_Title"] = "RobocopySW — Configurable backup",
        ["Main_New"] = "New",
        ["Main_Edit"] = "Edit",
        ["Main_Delete"] = "Delete",
        ["Main_MoveUpTip"] = "Move up (higher priority)",
        ["Main_MoveDownTip"] = "Move down (lower priority)",
        ["Main_Preview"] = "Preview",
        ["Main_RunSelected"] = "Run selected",
        ["Main_RunAll"] = "Run all",
        ["Main_Settings"] = "Settings",
        ["Main_LogTitle"] = "Execution log",
        ["Main_Clear"] = "Clear",

        ["Col_Active"] = "Enabled",
        ["Col_Name"] = "Name",
        ["Col_Mode"] = "Mode",
        ["Col_Source"] = "Source",
        ["Col_Dest"] = "Destination",
        ["Col_LastResult"] = "Last result",

        ["Empty_Title"] = "No jobs configured",
        ["Empty_Desc"] = "Create your first backup: pick a source folder and a destination.",
        ["Empty_Button"] = "Create the first job",

        ["Status_Running"] = "Running…",
        ["Status_JobsConfigured"] = "{0} configured jobs",

        ["Mode_Mirror"] = "Mirror",
        ["Mode_CopyOnly"] = "Copy only",

        ["Run_Preview"] = "PREVIEW",
        ["Run_Execution"] = "RUN",
        ["Run_PreviewStatus"] = "preview…",
        ["Run_InProgress"] = "running…",
        ["Run_OK"] = "OK",
        ["Run_Error"] = "ERROR",
        ["Run_Cancelled"] = "cancelled",
        ["Run_CancelledUser"] = "cancelled by user",
        ["Stat_Copied"] = "copied",
        ["Stat_Unchanged"] = "unchanged",
        ["Stat_Extra"] = "extra",
        ["Stat_Errors"] = "errors",

        ["Delete_Confirm"] = "Delete job '{0}'?",

        ["Editor_TitleEdit"] = "Edit job",
        ["Editor_NewJobName"] = "New job",
        ["Editor_Name"] = "Name",
        ["Editor_NamePlaceholder"] = "e.g. Documents",
        ["Editor_Source"] = "Source",
        ["Editor_Dest"] = "Destination",
        ["Editor_Mirror"] = "Mirror — makes the destination identical to the source (DELETES removed files)",
        ["Editor_MirrorHint"] = "Off = only copies and updates, never deletes at the destination.",
        ["Editor_ExcludeOlder"] = "Do not overwrite newer files at the destination (/XO)",
        ["Editor_CopyAll"] = "Also copy ACLs/owner (/COPYALL — useful on network shares)",
        ["Editor_BigJ"] = "Optimize large files (/J — unbuffered I/O)",
        ["Editor_BigJTip"] = "For very large files (multi-GB): bypasses the file system cache, often faster on SSD/NVMe and fast networks. Does not help small files. Mutually exclusive with restartable copy.",
        ["Editor_RestartZ"] = "Restartable copy (/Z — resumes if interrupted)",
        ["Editor_RestartZTip"] = "Resumes copying a large file if it gets interrupted (useful over VPN, Wi-Fi or unreliable networks). It has some overhead: on stable networks it is better left off. Mutually exclusive with large-file optimization.",
        ["Editor_Threads"] = "Threads (/MT)",
        ["Editor_ThreadsTip"] = "Number of files copied in parallel (multi-thread). Higher = faster, especially with many small files. 8 is fine almost always; 16+ on SSDs or fast network shares; on mechanical hard disks don't overdo it (it can thrash the head). 0 disables multi-thread.",
        ["Editor_Retries"] = "Retries (/R)",
        ["Editor_RetriesTip"] = "How many times to retry copying a file if the copy fails (e.g. a file temporarily in use or an unstable network). With often-open files keep a low value (1) to avoid long waits.",
        ["Editor_Wait"] = "Wait sec (/W)",
        ["Editor_WaitTip"] = "Seconds to wait between retries. Combines with Retries (e.g. 1 retry × 5 sec). High values greatly increase times if there are problematic files.",
        ["Editor_Credential"] = "Network credential (for UNC shares)",
        ["Editor_CredentialTip"] = "Only needed if the source or destination is a shared network folder (\\\\server\\share) requiring a username and password. Credentials are created in Settings → Credentials. For local or USB disks leave «none».",
        ["Editor_ExcludeFiles"] = "Exclude files (one per line)",
        ["Editor_ExcludeFilesTip"] = "File patterns NOT to copy, one per line. Examples: *.tmp (all .tmp), ~$* (Office temp files), *.log. Leave empty to copy everything.",
        ["Editor_ExcludeDirs"] = "Exclude folders (one per line)",
        ["Editor_ExcludeDirsTip"] = "Folder names to skip, one per line. Examples: cache, node_modules, Temp. They are excluded wherever they appear in the tree.",
        ["Editor_CommandPreview"] = "Robocopy command preview",
        ["Editor_Val_Name"] = "The job name is required.",
        ["Editor_Val_Source"] = "The source folder is required.",
        ["Editor_Val_Dest"] = "The destination folder is required.",
        ["Editor_BrowseTitle"] = "Select folder",
        ["Cred_NoneLocal"] = "(none — local path)",

        ["Settings_Title"] = "Settings",
        ["Tab_General"] = "General",
        ["Tab_Email"] = "Email",
        ["Tab_Credentials"] = "Network credentials",
        ["Tab_Schedule"] = "Scheduling",

        ["Set_Language"] = "Language",
        ["Lang_Auto"] = "Automatic (Windows)",
        ["Set_LogFolder"] = "Log folder (archived by date)",
        ["Set_TempFolder"] = "Temporary folder",
        ["Set_PathsHint"] = "Logs are ALWAYS on. If you leave these fields empty, logs and temporary files go to the default subfolders shown in grey (next to the app). Fill in a path only to move them elsewhere.",
        ["Set_Compress"] = "Compress logs to .zip",
        ["Set_Retention"] = "Log retention days (0 = never delete)",
        ["Set_CredSecurity"] = "Credential security",
        ["Set_CredScopeUser"] = "Encrypt passwords only for my Windows user (more secure)",
        ["Set_CredScopeHint"] = "On: passwords (credentials and email) decryptable only by your user; in exchange the scheduled task must run as your same user. Off: bound to the PC, work with any user (handy for scheduling). When you change the option, already-saved passwords are re-encrypted automatically.",

        ["Email_Enable"] = "Enable email notifications",
        ["Email_OnlyError"] = "Send only on error",
        ["Email_Smtp"] = "SMTP server",
        ["Email_Port"] = "Port",
        ["Email_Ssl"] = "Use SSL/TLS",
        ["Email_From"] = "From (sender)",
        ["Email_To"] = "To (recipient)",
        ["Email_User"] = "SMTP user (optional)",
        ["Email_Pwd"] = "SMTP password (leave blank to keep)",

        ["Cred_AddUpdate"] = "Add / update credential",
        ["Cred_Name"] = "Name",
        ["Cred_Host"] = "Host/Share (\\\\server\\share)",
        ["Cred_User"] = "User (DOMAIN\\user)",
        ["Cred_Pwd"] = "Password",
        ["Cred_Save"] = "Save credential",
        ["Cred_Remove"] = "Remove selected",
        ["Cred_ColUser"] = "User",
        ["Cred_NameRequired"] = "The credential name is required.",

        ["Sched_InfoTitle"] = "Automatic execution",
        ["Sched_InfoMsg"] = "Registers a task in Windows Task Scheduler that runs «Run all» silently at the chosen time.",
        ["Sched_Time"] = "Time (HH:mm)",
        ["Sched_Freq"] = "Frequency",
        ["Sched_Daily"] = "Daily",
        ["Sched_Weekly"] = "Weekly",
        ["Sched_Create"] = "Create / update task",
        ["Sched_Remove"] = "Remove task",
        ["Sched_BadTime"] = "Invalid time. Use the HH:mm format.",
        ["Sched_Created"] = "Scheduled task created/updated for {0}.",
        ["Sched_Removed"] = "Scheduled task removed.",
        ["Sched_Error"] = "Error: {0}",
    };
}
