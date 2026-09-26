using System.Text;
using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>
/// Copia della configurazione nella radice del disco di backup. Un disco che ha i file ma non i
/// job e' meta' backup: alla morte del PC la configurazione (job, esclusioni, pianificazioni,
/// email) non e' da nessuna parte, e va ricostruita a memoria. Dopo ogni run riuscito RoboKeep
/// scrive <c>&lt;radice&gt;\RoboKeep-config\</c> con la configurazione completa e un LEGGIMI che
/// spiega come si rimette in piedi.
/// <para>Best-effort in senso stretto: qualunque errore (disco in sola lettura, spazio finito,
/// permessi) diventa una riga nel log e nient'altro. Il backup dei file e' gia' riuscito e non
/// puo' diventare un fallimento per colpa di questa comodita'.</para>
/// </summary>
public static class ConfigMirror
{
    /// <summary>Nome della cartella nella radice del volume. Lo conosce anche
    /// <see cref="RobocopyArgsBuilder"/>: un job che ha come destinazione la radice stessa deve
    /// escluderla, altrimenti il mirror la cancellerebbe come file "extra".</summary>
    public const string FolderName = "RoboKeep-config";

    /// <summary>Nome del file di configurazione: lo stesso formato (e lo stesso nome) del
    /// config.json dell'app, cosi' l'importazione dalle Impostazioni lo accetta com'e'.</summary>
    public const string ConfigFileName = "config.json";

    /// <summary>Nome del LEGGIMI: chi trova il disco nel cassetto deve capire cos'e' senza RoboKeep.</summary>
    public const string ReadmeFileName = "LEGGIMI.txt";

    /// <summary>Cartella in cui va la copia per una destinazione, o null se non ce n'e' una:
    /// share UNC e unita' di rete (nessuna radice di volume da nominare, e la copia riguarda il
    /// disco che l'utente porta via) oppure percorsi da cui non si ricava nessuna radice.</summary>
    public static string? TargetFolder(string? destination)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(destination)) return null;
            destination = destination.Trim();
            // Solo percorsi completi ("E:\Backup", "\\nas\share\x"). Un percorso relativo — o con
            // uno spazio davanti, che e' lo stesso sbaglio scritto peggio — verrebbe risolto sulla
            // cartella di lavoro del processo, e la copia finirebbe nella radice del disco di
            // SISTEMA: l'unico posto in cui non deve mai andare.
            if (!Path.IsPathFullyQualified(destination)) return null;
            // Una share di rete non e' il disco che si stacca e si mette nel cassetto: la copia
            // li' non salverebbe nessuno e sporcherebbe la radice di un server.
            if (VolumeIdentity.IsNetworkPath(destination)) return null;

            var root = Path.GetPathRoot(Path.GetFullPath(destination));
            if (string.IsNullOrEmpty(root)) return null;
            if (root.StartsWith(@"\\", StringComparison.Ordinal)) return null;

            return Path.Combine(root, FolderName);
        }
        catch { return null; }
    }

    /// <summary>Scrive la copia nella radice del volume di <paramref name="destination"/>.
    /// Non lancia mai: se non c'e' una radice valida non fa nulla, e ogni errore di scrittura
    /// diventa una riga per <paramref name="progress"/>.</summary>
    public static void Write(AppConfig config, string destination, IProgress<string>? progress = null)
    {
        if (TargetFolder(destination) is { } folder)
            WriteTo(config, folder, progress, Environment.MachineName, DateTime.Now);
    }

    /// <summary>Come <see cref="Write"/>, ma con la cartella gia' risolta: la usa
    /// <see cref="BackupRunner"/>, che la calcola per conto suo (e nei test la reindirizza, per non
    /// scrivere nella radice del disco della macchina che esegue le prove).</summary>
    public static void WriteTo(
        AppConfig config, string targetFolder, IProgress<string>? progress, string machineName, DateTime when)
    {
        ArgumentNullException.ThrowIfNull(config);
        try
        {
            // Prima il testo, poi il disco. La finestra puo' aggiungere un job o una credenziale
            // mentre il backup gira, e una lista che cambia a serializzazione iniziata la fa
            // fallire: succedendo qui, l'errore resta in memoria, la copia precedente sul disco
            // resta valida e l'utente legge una riga nel log. Serializzare direttamente sul file
            // lascerebbe invece un config.json troncato — peggio che non averlo.
            var json = ConfigTransfer.Serialize(config);

            Directory.CreateDirectory(targetFolder);
            WriteIfChanged(Path.Combine(targetFolder, ConfigFileName), json, Utf8NoBom);
            // Con il BOM: il LEGGIMI si apre nel Blocco note di un PC qualsiasi, e senza BOM gli
            // editor piu' vecchi mostrano le lettere accentate al posto sbagliato.
            WriteIfChanged(Path.Combine(targetFolder, ReadmeFileName), ReadmeText(machineName, when), Utf8Bom);
            progress?.Report(string.Format(CoreLoc.S("ConfigCopy_Written"), targetFolder));
        }
        catch (Exception ex)
        {
            progress?.Report(string.Format(CoreLoc.S("ConfigCopy_Failed"), ex.Message));
        }
    }

    // Lo stesso config.json dell'app: UTF-8 senza BOM. Il LEGGIMI invece lo legge un essere umano
    // nel Blocco note, e li' il BOM evita le lettere accentate sbagliate.
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly UTF8Encoding Utf8Bom = new(encoderShouldEmitUTF8Identifier: true);

    /// <summary>Scrive solo se il contenuto e' cambiato, e in modo atomico: prima un
    /// <c>.tmp</c> accanto, poi uno spostamento sopra il file buono. Chi apre la cartella trova
    /// sempre la versione vecchia o quella nuova, mai una a meta': un'interruzione (disco staccato,
    /// PC spento) lascia il <c>.tmp</c>, non un config.json troncato — che sarebbe illeggibile
    /// proprio nel momento in cui serve.</summary>
    private static void WriteIfChanged(string path, string content, Encoding encoding)
    {
        // La copia viene riscritta a ogni backup riuscito, e di solito e' identica: riscriverla
        // consumerebbe la memoria flash del disco e cambierebbe la data per niente.
        if (SameContent(path, content)) return;

        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content, encoding);
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>true solo se il file esiste e contiene gia' esattamente questo testo. Qualunque
    /// problema di lettura risponde false: riscrivere e' la scelta sicura.</summary>
    private static bool SameContent(string path, string content)
    {
        try { return File.Exists(path) && File.ReadAllText(path) == content; }
        catch { return false; }
    }

    /// <summary>Testo del LEGGIMI, italiano e inglese nello stesso file: chi lo legge potrebbe non
    /// essere chi ha fatto il backup, e non c'e' nessuna lingua da indovinare.</summary>
    public static string ReadmeText(string machineName, DateTime when)
    {
        var stamp = when.ToString("dd/MM/yyyy HH:mm");
        var sb = new StringBuilder();

        sb.AppendLine("=== RoboKeep — copia della configurazione ===");
        sb.AppendLine();
        sb.AppendLine("Questa cartella contiene la configurazione di RoboKeep del PC che ha fatto il backup");
        sb.AppendLine("su questo disco: job, esclusioni, pianificazioni, impostazioni e notifiche email.");
        sb.AppendLine("NON è una copia dei tuoi file: quelli sono nelle altre cartelle del disco.");
        sb.AppendLine();
        sb.AppendLine($"PC di origine : {machineName}");
        sb.AppendLine($"Scritta il    : {stamp}");
        sb.AppendLine();
        sb.AppendLine("Come si ripristina:");
        sb.AppendLine("  1. installa RoboKeep sul PC (https://github.com/robisera-ai/RoboKeep);");
        sb.AppendLine("  2. apri RoboKeep → Impostazioni → Pianificazione → «Importa configurazione»;");
        sb.AppendLine($"  3. scegli il file {ConfigFileName} che si trova in questa cartella.");
        sb.AppendLine();
        sb.AppendLine("Le password (share di rete, email) sono cifrate con DPAPI di Windows: si decifrano");
        sb.AppendLine("solo su quel PC (e solo con il tuo utente se nelle Impostazioni hai scelto «cifra le");
        sb.AppendLine("password solo per il mio utente Windows»). Su un altro PC vanno reinserite a mano una");
        sb.AppendLine("volta; tutto il resto torna com'era.");
        sb.AppendLine();
        sb.AppendLine("Questa copia viene riscritta a ogni backup riuscito. Se non la vuoi, spegni");
        sb.AppendLine("«Salva una copia della configurazione sui dischi di backup» nelle Impostazioni.");
        sb.AppendLine();
        sb.AppendLine("=== RoboKeep — configuration copy ===");
        sb.AppendLine();
        sb.AppendLine("This folder holds the RoboKeep configuration of the PC that backed up to this disk:");
        sb.AppendLine("jobs, exclusions, schedules, settings and email notifications.");
        sb.AppendLine("It is NOT a copy of your files: those are in the other folders of this disk.");
        sb.AppendLine();
        sb.AppendLine($"Source PC  : {machineName}");
        sb.AppendLine($"Written on : {stamp}");
        sb.AppendLine();
        sb.AppendLine("How to restore it:");
        sb.AppendLine("  1. install RoboKeep on the PC (https://github.com/robisera-ai/RoboKeep);");
        sb.AppendLine("  2. open RoboKeep → Settings → Scheduling → \"Import configuration\";");
        sb.AppendLine($"  3. pick the {ConfigFileName} file in this folder.");
        sb.AppendLine();
        sb.AppendLine("Passwords (network shares, email) are encrypted with Windows DPAPI: they can only be");
        sb.AppendLine("decrypted on that PC (and only by your Windows user if you chose \"encrypt passwords");
        sb.AppendLine("only for my Windows user\" in Settings). On another PC you have to type them in once;");
        sb.AppendLine("everything else comes back as it was.");
        sb.AppendLine();
        sb.AppendLine("This copy is rewritten after every successful backup. If you do not want it, turn off");
        sb.AppendLine("\"Save a copy of the configuration on the backup disks\" in Settings.");

        return sb.ToString();
    }
}
