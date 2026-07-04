namespace RoboKeep.Core.Models;

/// <summary>
/// Definizione di un singolo job di backup: una coppia sorgente→destinazione
/// con le relative opzioni, che verranno tradotte in argomenti robocopy.
/// </summary>
public sealed class BackupJob
{
    /// <summary>Nome univoco del job (usato anche da CLI e nel nome dei log).</summary>
    public string Name { get; set; } = "";

    /// <summary>Cartella sorgente.</summary>
    public string Source { get; set; } = "";

    /// <summary>Cartella destinazione.</summary>
    public string Destination { get; set; } = "";

    /// <summary>
    /// true → mirror (<c>/MIR</c>): la destinazione diventa identica alla sorgente,
    /// inclusa la cancellazione dei file/cartelle non più presenti in sorgente.
    /// false → <c>/E</c>: copia/aggiorna ricorsivo senza cancellare nulla.
    /// </summary>
    public bool Mirror { get; set; } = true;

    /// <summary>true → aggiunge <c>/XO</c>: non sovrascrive in destinazione i file più recenti.</summary>
    public bool ExcludeOlder { get; set; }

    /// <summary>
    /// true → <c>/COPYALL</c> (dati, attributi, timestamp, sicurezza, owner, audit), utile su share NTFS.
    /// false → <c>/COPY:DAT</c> (dati, attributi, timestamp), come nei job storici.
    /// </summary>
    public bool CopyAll { get; set; }

    /// <summary>Numero di thread per <c>/MT:n</c>. 0 o meno = multi-thread disattivato.</summary>
    public int MultiThread { get; set; } = 8;

    /// <summary>
    /// <c>/J</c>: I/O non bufferizzato, ottimizza i file molto grandi (multi-GB).
    /// Mutuamente esclusivo con <see cref="Restartable"/>.
    /// </summary>
    public bool UnbufferedIO { get; set; }

    /// <summary>
    /// <c>/Z</c>: modalità riavviabile, riprende la copia interrotta di un file grande.
    /// Mutuamente esclusivo con <see cref="UnbufferedIO"/>.
    /// </summary>
    public bool Restartable { get; set; }

    /// <summary><c>/V</c>: registra nel log <b>tutti</b> i file, inclusi quelli identici/saltati
    /// (output verboso). Default false: il log elenca solo i file copiati/extra.</summary>
    public bool LogAllFiles { get; set; }

    /// <summary>Pattern di file da escludere (<c>/XF</c>), es. <c>*.tmp</c>.</summary>
    public List<string> ExcludeFiles { get; set; } = new();

    /// <summary>Cartelle da escludere (<c>/XD</c>), es. <c>cache</c>.</summary>
    public List<string> ExcludeDirs { get; set; } = new();

    /// <summary>Pattern di file da forzare in copia anche se data/dimensione non cambiano
    /// (seconda passata robocopy con /IS /IT). Es. *.pst, database.dat. Vuoto = feature disattivata.</summary>
    public List<string> ForceCopyFiles { get; set; } = new();

    /// <summary>Modalità della lista <see cref="ForceCopyFiles"/>:
    /// false = "copia sempre" (ricopia integrale a ogni run);
    /// true = "smart" (ricopia solo i file il cui hash è cambiato dall'ultimo backup).</summary>
    public bool ForceCopySmart { get; set; }

    /// <summary>Numero di tentativi su errore (<c>/R:n</c>).</summary>
    public int Retries { get; set; } = 1;

    /// <summary>Secondi di attesa tra i tentativi (<c>/W:n</c>).</summary>
    public int Wait { get; set; } = 5;

    /// <summary>Se false il job viene saltato dalle esecuzioni "tutti".</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Id facoltativo della credenziale di rete da usare per le share UNC.</summary>
    public string? CredentialId { get; set; }

    /// <summary>true → mantiene snapshot datati con hard-link (versioning). Richiede destinazione NTFS locale.</summary>
    public bool Versioned { get; set; }

    /// <summary>Numero massimo di snapshot da conservare. 0 = nessun limite di numero.</summary>
    public int SnapshotKeepCount { get; set; }

    /// <summary>Elimina gli snapshot più vecchi di questi giorni. 0 = nessun limite di età.</summary>
    public int SnapshotMaxAgeDays { get; set; }

    /// <summary>true → prima della copia crea uno snapshot VSS del volume sorgente e copia da lì,
    /// così anche i file aperti/bloccati vengono letti integri. Richiede sorgente su volume NTFS
    /// locale e conferma amministratore (UAC) all'avvio del run.</summary>
    public bool UseVss { get; set; }
}
