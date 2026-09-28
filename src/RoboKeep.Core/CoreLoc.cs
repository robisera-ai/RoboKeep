using System.Globalization;

namespace RoboKeep.Core;

/// <summary>
/// Localizzazione dei testi generati da Core (esiti, riepiloghi, email) in 5 lingue,
/// basata sulla cultura UI corrente del thread (impostata dall'app). Fallback all'inglese.
/// </summary>
public static class CoreLoc
{
    private static string? _forced;

    /// <summary>Imposta esplicitamente la lingua dei testi Core (chiamata dall'app quando cambia la
    /// lingua UI). Più affidabile della cultura del thread: i report/email vengono generati su thread
    /// di background del pool, che possono avere una cultura diversa da quella impostata.</summary>
    public static void SetLanguage(string lang) =>
        _forced = lang is "it" or "es" or "fr" or "de" or "en" ? lang : null;

    private static string Lang => _forced ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
    {
        "it" => "it",
        "es" => "es",
        "fr" => "fr",
        "de" => "de",
        _ => "en",
    };

    /// <summary>Testo localizzato per la chiave; fallback su inglese, poi sulla chiave.</summary>
    public static string S(string key)
    {
        if (Map.TryGetValue(key, out var byLang))
        {
            if (byLang.TryGetValue(Lang, out var v)) return v;
            if (byLang.TryGetValue("en", out var e)) return e;
        }
        return key;
    }

    private static Dictionary<string, string> L(string it, string en, string es, string fr, string de) =>
        new() { ["it"] = it, ["en"] = en, ["es"] = es, ["fr"] = fr, ["de"] = de };

    private static readonly Dictionary<string, Dictionary<string, string>> Map = new()
    {
        ["Exit_InvalidSummary"] = L(
            "Errore: codice di uscita non valido.", "Error: invalid exit code.",
            "Error: código de salida no válido.", "Erreur : code de sortie non valide.",
            "Fehler: ungültiger Exit-Code."),
        ["Exit_InvalidDetail"] = L(
            "Codice di uscita negativo/non valido.", "Negative/invalid exit code.",
            "Código de salida negativo/no válido.", "Code de sortie négatif/non valide.",
            "Negativer/ungültiger Exit-Code."),
        ["Exit_Copied"] = L(
            "File e/o cartelle copiati correttamente.", "Files and/or folders copied successfully.",
            "Archivos y/o carpetas copiados correctamente.", "Fichiers et/ou dossiers copiés avec succès.",
            "Dateien und/oder Ordner erfolgreich kopiert."),
        ["Exit_Extra"] = L(
            "Rilevati elementi extra in destinazione (in mirror vengono rimossi).",
            "Extra items detected at the destination (removed in mirror mode).",
            "Elementos extra detectados en el destino (se eliminan en modo espejo).",
            "Éléments en trop détectés à la destination (supprimés en mode miroir).",
            "Zusätzliche Elemente am Ziel erkannt (im Spiegelmodus entfernt)."),
        ["Exit_Mismatch"] = L(
            "Rilevati elementi non corrispondenti (mismatch): verifica consigliata.",
            "Mismatched items detected: review recommended.",
            "Elementos no coincidentes detectados: se recomienda revisar.",
            "Éléments non concordants détectés : vérification recommandée.",
            "Nicht übereinstimmende Elemente erkannt: Überprüfung empfohlen."),
        ["Exit_Errors"] = L(
            "Errori durante la copia di alcuni file (tentativi esauriti).",
            "Errors copying some files (retries exhausted).",
            "Errores al copiar algunos archivos (reintentos agotados).",
            "Erreurs lors de la copie de certains fichiers (tentatives épuisées).",
            "Fehler beim Kopieren einiger Dateien (Wiederholungen erschöpft)."),
        ["Exit_Fatal"] = L(
            "Errore grave: robocopy non ha potuto copiare nulla.",
            "Serious error: robocopy could not copy anything.",
            "Error grave: robocopy no pudo copiar nada.",
            "Erreur grave : robocopy n'a rien pu copier.",
            "Schwerer Fehler: robocopy konnte nichts kopieren."),
        ["Exit_NoChange"] = L(
            "Nessuna modifica necessaria: sorgente e destinazione già allineate.",
            "No changes needed: source and destination already in sync.",
            "No se necesitan cambios: origen y destino ya sincronizados.",
            "Aucune modification nécessaire : source et destination déjà synchronisées.",
            "Keine Änderungen nötig: Quelle und Ziel bereits synchron."),
        ["Exit_Success"] = L(
            "Completato con successo.", "Completed successfully.",
            "Completado correctamente.", "Terminé avec succès.", "Erfolgreich abgeschlossen."),
        ["Exit_WithErrors"] = L(
            "Completato con errori.", "Completed with errors.",
            "Completado con errores.", "Terminé avec des erreurs.", "Mit Fehlern abgeschlossen."),

        ["Run_CancelledRecap"] = L(
            "====== ANNULLATO DALL'UTENTE alle {0:HH:mm:ss} (RoboKeep) ======",
            "====== CANCELLED BY THE USER at {0:HH:mm:ss} (RoboKeep) ======",
            "====== CANCELADO POR EL USUARIO a las {0:HH:mm:ss} (RoboKeep) ======",
            "====== ANNULÉ PAR L'UTILISATEUR à {0:HH:mm:ss} (RoboKeep) ======",
            "====== VOM BENUTZER ABGEBROCHEN um {0:HH:mm:ss} (RoboKeep) ======"),

        ["Recap_TitlePreview"] = L(
            "RIEPILOGO ANTEPRIMA (RoboKeep)", "PREVIEW SUMMARY (RoboKeep)",
            "RESUMEN VISTA PREVIA (RoboKeep)", "RÉSUMÉ APERÇU (RoboKeep)",
            "VORSCHAU-ZUSAMMENFASSUNG (RoboKeep)"),
        ["Recap_Title"] = L(
            "RIEPILOGO (RoboKeep)", "SUMMARY (RoboKeep)",
            "RESUMEN (RoboKeep)", "RÉSUMÉ (RoboKeep)", "ZUSAMMENFASSUNG (RoboKeep)"),
        ["Recap_ExtraDry"] = L(
            "  (verrebbero rimossi in mirror)", "  (would be removed in mirror)",
            "  (se eliminarían en modo espejo)", "  (seraient supprimés en miroir)",
            "  (würden im Spiegelmodus entfernt)"),
        ["Recap_ExtraReal"] = L(
            "  (in dest, non in sorgente)", "  (in dest, not in source)",
            "  (en destino, no en origen)", "  (dans la destination, pas dans la source)",
            "  (im Ziel, nicht in der Quelle)"),

        ["Vss_Created"] = L(
            "[vss] snapshot creato ({0}): copio dai file congelati, inclusi quelli aperti.",
            "[vss] snapshot created ({0}): copying from frozen files, including open ones.",
            "[vss] instantánea creada ({0}): copio desde los archivos congelados, incluidos los abiertos.",
            "[vss] instantané créé ({0}) : copie depuis les fichiers figés, y compris ceux ouverts.",
            "[vss] Snapshot erstellt ({0}): Kopie aus eingefrorenen Dateien, auch geöffnete."),
        ["Vss_Released"] = L(
            "[vss] snapshot rilasciato.",
            "[vss] snapshot released.",
            "[vss] instantánea liberada.",
            "[vss] instantané libéré.",
            "[vss] Snapshot freigegeben."),
        ["Vss_Unavailable"] = L(
            "[vss] ATTENZIONE: snapshot non disponibile ({0}): i file aperti verranno saltati.",
            "[vss] WARNING: snapshot unavailable ({0}): open files will be skipped.",
            "[vss] ATENCIÓN: instantánea no disponible ({0}): los archivos abiertos se omitirán.",
            "[vss] ATTENTION : instantané indisponible ({0}) : les fichiers ouverts seront ignorés.",
            "[vss] ACHTUNG: Snapshot nicht verfügbar ({0}): geöffnete Dateien werden übersprungen."),
        ["Vss_UacDenied"] = L(
            "conferma amministratore negata",
            "administrator approval denied",
            "aprobación de administrador denegada",
            "approbation administrateur refusée",
            "Administratorbestätigung verweigert"),
        ["Vss_NotEligible"] = L(
            "la sorgente non è su un volume locale NTFS",
            "the source is not on a local NTFS volume",
            "el origen no está en un volumen NTFS local",
            "la source n'est pas sur un volume NTFS local",
            "die Quelle liegt nicht auf einem lokalen NTFS-Volume"),

        ["Verify_Start"] = L(
            "[verifica] confronto hash sorgente-destinazione ({0} file)...",
            "[verify] comparing source-destination hashes ({0} files)...",
            "[verificación] comparando hashes origen-destino ({0} archivos)...",
            "[vérification] comparaison des empreintes source-destination ({0} fichiers)...",
            "[Prüfung] Vergleiche Quell-Ziel-Hashes ({0} Dateien)..."),
        ["Verify_Progress"] = L(
            "[verifica] verificati {0}/{1}...",
            "[verify] checked {0}/{1}...",
            "[verificación] verificados {0}/{1}...",
            "[vérification] vérifiés {0}/{1}...",
            "[Prüfung] geprüft {0}/{1}..."),
        ["Verify_RecapOk"] = L(
            "[verifica] OK: {0} file identici, {1} modificati dopo il backup, {2} saltati (in uso), {3} mancanti.",
            "[verify] OK: {0} identical files, {1} changed after the backup, {2} skipped (in use), {3} missing.",
            "[verificación] OK: {0} archivos idénticos, {1} modificados tras la copia, {2} omitidos (en uso), {3} ausentes.",
            "[vérification] OK : {0} fichiers identiques, {1} modifiés après la sauvegarde, {2} ignorés (utilisés), {3} manquants.",
            "[Prüfung] OK: {0} identische Dateien, {1} nach dem Backup geändert, {2} übersprungen (in Benutzung), {3} fehlend."),
        ["Verify_RecapBad"] = L(
            "[verifica] ATTENZIONE: {0} file DIFFERENTI dalla sorgente! Primi file: {1}",
            "[verify] WARNING: {0} files DIFFERENT from the source! First files: {1}",
            "[verificación] ATENCIÓN: ¡{0} archivos DIFERENTES del origen! Primeros: {1}",
            "[vérification] ATTENTION : {0} fichiers DIFFÉRENTS de la source ! Premiers : {1}",
            "[Prüfung] ACHTUNG: {0} Dateien WEICHEN von der Quelle AB! Erste Dateien: {1}"),
        ["Verify_NothingToVerify"] = L(
            "[verifica] niente da verificare: il job versionato non ha ancora snapshot.",
            "[verify] nothing to verify: the versioned job has no snapshots yet.",
            "[verificación] nada que verificar: el trabajo versionado aún no tiene instantáneas.",
            "[vérification] rien à vérifier : la tâche versionnée n'a pas encore d'instantanés.",
            "[Prüfung] nichts zu prüfen: der versionierte Job hat noch keine Snapshots."),
        ["Verify_LogTitle"] = L(
            "VERIFICA INTEGRITÀ", "INTEGRITY VERIFICATION", "VERIFICACIÓN DE INTEGRIDAD",
            "VÉRIFICATION D'INTÉGRITÉ", "INTEGRITÄTSPRÜFUNG"),
        // Finisce nel nome del file: solo lettere semplici, niente spazi né accenti.
        ["Verify_LogSuffix"] = L("verifica", "verify", "verificacion", "verification", "pruefung"),
        ["Verify_LogMismatchList"] = L(
            "File DIFFERENTI dalla sorgente (elenco completo, fino a 50):",
            "Files DIFFERENT from the source (full list, up to 50):",
            "Archivos DIFERENTES del origen (lista completa, hasta 50):",
            "Fichiers DIFFÉRENTS de la source (liste complète, jusqu'à 50) :",
            "Dateien, die von der Quelle ABWEICHEN (vollständige Liste, bis zu 50):"),
        ["Verify_NotDue"] = L(
            "[verifica] non prevista oggi: l'ultima è del {0}, la prossima tra {1} giorni.",
            "[verify] not scheduled today: the last one was on {0}, the next is in {1} days.",
            "[verificación] no prevista hoy: la última fue el {0}, la próxima en {1} días.",
            "[vérification] non prévue aujourd'hui : la dernière date du {0}, la prochaine dans {1} jours.",
            "[Prüfung] heute nicht vorgesehen: die letzte war am {0}, die nächste in {1} Tagen."),
        ["Versioning_NoHardLink"] = L(
            "[versioning] ATTENZIONE: la destinazione non supporta gli hard-link. Eseguo un mirror semplice (nessuno snapshot). Usa una destinazione NTFS locale per le versioni.",
            "[versioning] WARNING: the destination does not support hard-links. Running a plain mirror (no snapshot). Use a local NTFS destination for versions.",
            "[versionado] ATENCIÓN: el destino no admite enlaces duros. Ejecuto un espejo simple (sin instantánea). Usa un destino NTFS local para las versiones.",
            "[versioning] ATTENTION : la destination ne prend pas en charge les liens physiques. Miroir simple exécuté (pas d'instantané). Utilisez une destination NTFS locale pour les versions.",
            "[Versionierung] ACHTUNG: das Ziel unterstützt keine Hardlinks. Einfache Spiegelung wird ausgeführt (kein Snapshot). Verwenden Sie ein lokales NTFS-Ziel für Versionen."),
        ["Versioning_SkipFile"] = L(
            "[versioning] file del vecchio snapshot non leggibile, saltato (verrà ricopiato dalla sorgente): {0}",
            "[versioning] file in the previous snapshot could not be read, skipped (it will be recopied from the source): {0}",
            "[versionado] archivo de la instantánea anterior ilegible, omitido (se volverá a copiar desde el origen): {0}",
            "[versioning] fichier de l'instantané précédent illisible, ignoré (il sera recopié depuis la source) : {0}",
            "[Versionierung] Datei im vorherigen Snapshot nicht lesbar, übersprungen (wird aus der Quelle neu kopiert): {0}"),
        ["Versioning_SkipSummary"] = L(
            "[versioning] {0} file del vecchio snapshot saltati (bloccati o non accessibili): verranno ricopiati dalla sorgente.",
            "[versioning] {0} files skipped from the previous snapshot (locked or not accessible): they will be recopied from the source.",
            "[versionado] {0} archivos omitidos de la instantánea anterior (bloqueados o inaccesibles): se volverán a copiar desde el origen.",
            "[versioning] {0} fichiers ignorés de l'instantané précédent (verrouillés ou inaccessibles) : ils seront recopiés depuis la source.",
            "[Versionierung] {0} Dateien aus dem vorherigen Snapshot übersprungen (gesperrt oder nicht zugänglich): sie werden aus der Quelle neu kopiert."),

        ["Versioning_Adopted"] = L(
            "[versioning] trovata una copia semplice già presente nella destinazione: diventa la prima versione ({0}, {1} elementi spostati, nessuna ricopia). Da ora si copia solo ciò che cambia.",
            "[versioning] found an existing plain copy in the destination: it becomes the first version ({0}, {1} items moved, nothing recopied). From now on only changes are copied.",
            "[versionado] se encontró una copia simple ya presente en el destino: pasa a ser la primera versión ({0}, {1} elementos movidos, nada recopiado). Desde ahora solo se copia lo que cambia.",
            "[versioning] copie simple déjà présente dans la destination : elle devient la première version ({0}, {1} éléments déplacés, rien recopié). Désormais seuls les changements sont copiés.",
            "[Versionierung] vorhandene einfache Kopie im Ziel gefunden: sie wird zur ersten Version ({0}, {1} Elemente verschoben, nichts neu kopiert). Ab jetzt wird nur kopiert, was sich ändert."),
        ["Versioning_NotAdopted"] = L(
            "[versioning] nella destinazione ci sono elementi che non esistono nella sorgente (es. {0}; {1} in tutto): non è una copia di questo job, quindi non viene adottata come prima versione. Restano dove sono; la prima versione parte da zero.",
            "[versioning] the destination contains items that do not exist in the source (e.g. {0}; {1} in total): it is not a copy made by this job, so it is not adopted as the first version. They stay where they are; the first version starts from scratch.",
            "[versionado] el destino contiene elementos que no existen en el origen (p. ej. {0}; {1} en total): no es una copia de este trabajo, así que no se adopta como primera versión. Se quedan donde están; la primera versión empieza desde cero.",
            "[versioning] la destination contient des éléments absents de la source (ex. {0} ; {1} au total) : ce n'est pas une copie faite par cette tâche, elle n'est donc pas adoptée comme première version. Ils restent en place ; la première version part de zéro.",
            "[Versionierung] das Ziel enthält Elemente, die es in der Quelle nicht gibt (z. B. {0}; {1} insgesamt): es ist keine Kopie dieses Jobs und wird daher nicht als erste Version übernommen. Sie bleiben, wo sie sind; die erste Version beginnt von vorn."),
        ["Versioning_NoService"] = L(
            "[versioni] job NON eseguito: in {0} ci sono già versioni, ma questa esecuzione di RoboKeep non è in grado di gestirle. Un mirror semplice le cancellerebbe come file extra, quindi non è stato toccato niente. Aggiorna RoboKeep o avvialo dalla finestra principale.",
            "[versions] job NOT run: {0} already holds versions, but this run of RoboKeep cannot handle them. A plain mirror would delete them as extra files, so nothing was touched. Update RoboKeep or start it from the main window.",
            "[versiones] trabajo NO ejecutado: en {0} ya hay versiones, pero esta ejecución de RoboKeep no puede gestionarlas. Un espejo simple las eliminaría como archivos extra, así que no se ha tocado nada. Actualiza RoboKeep o inícialo desde la ventana principal.",
            "[versions] tâche NON exécutée : {0} contient déjà des versions, mais cette exécution de RoboKeep ne sait pas les gérer. Un miroir simple les supprimerait comme fichiers en trop, rien n'a donc été touché. Mettez RoboKeep à jour ou lancez-le depuis la fenêtre principale.",
            "[Versionen] Job NICHT ausgeführt: in {0} liegen bereits Versionen, aber dieser RoboKeep-Lauf kann sie nicht verwalten. Eine einfache Spiegelung würde sie als zusätzliche Dateien löschen, daher wurde nichts angetastet. Aktualisieren Sie RoboKeep oder starten Sie es aus dem Hauptfenster."),
        ["Versioning_NoServiceStatus"] = L(
            "Non eseguito: versioni presenti ma non gestibili.",
            "Not run: versions present but not manageable.",
            "No ejecutado: hay versiones pero no se pueden gestionar.",
            "Non exécutée : versions présentes mais non gérables.",
            "Nicht ausgeführt: Versionen vorhanden, aber nicht verwaltbar."),
        ["Versioning_PreviewAgainst"] = L(
            "[versioni] anteprima confrontata con {0}, che è la cartella su cui scrive il run vero. Le cartelle delle versioni non vengono toccate né conteggiate.",
            "[versions] preview compared against {0}, the folder the real run writes to. The version folders are neither touched nor counted.",
            "[versiones] vista previa comparada con {0}, la carpeta en la que escribe la ejecución real. Las carpetas de versiones no se tocan ni se cuentan.",
            "[versions] aperçu comparé à {0}, le dossier dans lequel écrit l'exécution réelle. Les dossiers de versions ne sont ni touchés ni comptés.",
            "[Versionen] Vorschau verglichen mit {0}, dem Ordner, in den der echte Lauf schreibt. Die Versionsordner werden weder angetastet noch gezählt."),
        ["Versioning_Checking"] = L(
            "[versioning] controllo se è cambiato qualcosa rispetto all'ultima versione...",
            "[versioning] checking whether anything changed since the last version...",
            "[versionado] compruebo si algo ha cambiado desde la última versión...",
            "[versioning] vérification des changements depuis la dernière version...",
            "[Versionierung] prüfe, ob sich seit der letzten Version etwas geändert hat..."),
        ["Versioning_NoChanges"] = L(
            "[versioning] nessuna modifica dall'ultima versione ({0}): non creo un nuovo snapshot identico. Il backup è già aggiornato.",
            "[versioning] nothing changed since the last version ({0}): no identical new snapshot is created. The backup is already up to date.",
            "[versionado] ningún cambio desde la última versión ({0}): no creo una nueva instantánea idéntica. La copia ya está al día.",
            "[versioning] aucun changement depuis la dernière version ({0}) : pas de nouvel instantané identique. La sauvegarde est déjà à jour.",
            "[Versionierung] keine Änderung seit der letzten Version ({0}): kein identischer neuer Snapshot. Das Backup ist bereits aktuell."),

        // --- Modello di versioni PER DIFFERENZA (destinazioni senza hard-link) ---
        ["Diff_Adopted"] = L(
            "[versioni] trovata una copia semplice già presente nella destinazione: diventa il backup corrente, spostata in «current» ({0} elementi, nessuna ricopia). Da ora ogni backup mette da parte in «versions» solo i file che sostituisce o cancella.",
            "[versions] found an existing plain copy in the destination: it becomes the current backup, moved into \"current\" ({0} items, nothing recopied). From now on every backup sets aside in \"versions\" only the files it replaces or deletes.",
            "[versiones] se encontró una copia simple ya presente en el destino: pasa a ser la copia actual, movida a «current» ({0} elementos, nada recopiado). Desde ahora cada copia aparta en «versions» solo los archivos que sustituye o elimina.",
            "[versions] copie simple déjà présente dans la destination : elle devient la sauvegarde courante, déplacée dans « current » ({0} éléments, rien recopié). Désormais chaque sauvegarde met de côté dans « versions » uniquement les fichiers qu'elle remplace ou supprime.",
            "[Versionen] vorhandene einfache Kopie im Ziel gefunden: sie wird zum aktuellen Backup und nach „current“ verschoben ({0} Elemente, nichts neu kopiert). Ab jetzt legt jedes Backup in „versions“ nur die Dateien beiseite, die es ersetzt oder löscht."),
        ["Diff_Moved"] = L(
            "[versioni] {0} file e {1} cartelle messi da parte nella versione {2}",
            "[versions] {0} files and {1} folders set aside in version {2}",
            "[versiones] {0} archivos y {1} carpetas apartados en la versión {2}",
            "[versions] {0} fichiers et {1} dossiers mis de côté dans la version {2}",
            "[Versionen] {0} Dateien und {1} Ordner in Version {2} beiseitegelegt"),
        ["Diff_MoveFailed"] = L(
            "[versioni] {0} non messo da parte ({1}): il backup lo sovrascrive lo stesso, ma questa versione non ne conterrà la copia precedente.",
            "[versions] {0} not set aside ({1}): the backup overwrites it anyway, but this version will not hold its previous copy.",
            "[versiones] {0} no apartado ({1}): la copia lo sobrescribe igualmente, pero esta versión no contendrá su copia anterior.",
            "[versions] {0} non mis de côté ({1}) : la sauvegarde l'écrase quand même, mais cette version ne contiendra pas sa copie précédente.",
            "[Versionen] {0} nicht beiseitegelegt ({1}): das Backup überschreibt sie trotzdem, aber diese Version enthält ihre vorherige Kopie nicht."),
        ["Diff_Created"] = L(
            "[versioni] versione {0} creata: contiene i file sostituiti o cancellati da questo backup.",
            "[versions] version {0} created: it holds the files this backup replaced or deleted.",
            "[versiones] versión {0} creada: contiene los archivos que esta copia ha sustituido o eliminado.",
            "[versions] version {0} créée : elle contient les fichiers que cette sauvegarde a remplacés ou supprimés.",
            "[Versionen] Version {0} erstellt: sie enthält die Dateien, die dieses Backup ersetzt oder gelöscht hat."),
        ["Diff_PromotedPartial"] = L(
            "[versioni] backup non riuscito, ma la versione {0} resta: è incompleta e utilizzabile, contiene gli stati precedenti dei file che erano già stati messi da parte (per quelli cancellati dalla sorgente è l'unica copia rimasta). Non viene cancellata.",
            "[versions] the backup failed, but version {0} stays: it is incomplete and usable, holding the previous states of the files already set aside (for those deleted from the source it is the only copy left). It is not deleted.",
            "[versiones] la copia ha fallado, pero la versión {0} se queda: está incompleta y es utilizable, contiene los estados anteriores de los archivos ya apartados (para los eliminados del origen es la única copia que queda). No se elimina.",
            "[versions] la sauvegarde a échoué, mais la version {0} reste : elle est incomplète et utilisable, elle contient les états précédents des fichiers déjà mis de côté (pour ceux supprimés de la source, c'est la seule copie restante). Elle n'est pas supprimée.",
            "[Versionen] das Backup ist fehlgeschlagen, aber Version {0} bleibt: sie ist unvollständig und nutzbar und enthält die früheren Zustände der bereits beiseitegelegten Dateien (für die aus der Quelle gelöschten ist sie die einzige verbliebene Kopie). Sie wird nicht gelöscht."),
        ["Diff_Recovered"] = L(
            "[versioni] recuperata la versione {0} lasciata da un run interrotto: conteneva file già messi da parte, quindi diventa una versione vera invece di essere cancellata.",
            "[versions] recovered version {0}, left behind by an interrupted run: it held files already set aside, so it becomes a real version instead of being deleted.",
            "[versiones] recuperada la versión {0}, dejada por una ejecución interrumpida: contenía archivos ya apartados, así que pasa a ser una versión real en vez de eliminarse.",
            "[versions] version {0} récupérée, laissée par une exécution interrompue : elle contenait des fichiers déjà mis de côté, elle devient donc une vraie version au lieu d'être supprimée.",
            "[Versionen] Version {0} wiederhergestellt, von einem abgebrochenen Lauf zurückgelassen: sie enthielt bereits beiseitegelegte Dateien und wird daher zu einer echten Version, statt gelöscht zu werden."),
        ["Diff_RenameFailed"] = L(
            "[versioni] la versione {0} non si è potuta rinominare al nome definitivo ({1}): resta com'è, con i suoi file, e il prossimo run la recupera. Niente è andato perso.",
            "[versions] version {0} could not be renamed to its final name ({1}): it stays as it is, with its files, and the next run recovers it. Nothing was lost.",
            "[versiones] la versión {0} no se ha podido renombrar a su nombre definitivo ({1}): se queda como está, con sus archivos, y la próxima ejecución la recupera. No se ha perdido nada.",
            "[versions] la version {0} n'a pas pu être renommée avec son nom définitif ({1}) : elle reste telle quelle, avec ses fichiers, et la prochaine exécution la récupère. Rien n'a été perdu.",
            "[Versionen] Version {0} konnte nicht auf den endgültigen Namen umbenannt werden ({1}): sie bleibt mit ihren Dateien wie sie ist, und der nächste Lauf holt sie zurück. Nichts ist verloren."),
        ["Diff_StaleRemoved"] = L(
            "[versioni] rimossa la cartella vuota {0} lasciata da un run interrotto.",
            "[versions] removed the empty folder {0} left behind by an interrupted run.",
            "[versiones] eliminada la carpeta vacía {0} dejada por una ejecución interrumpida.",
            "[versions] dossier vide {0} laissé par une exécution interrompue supprimé.",
            "[Versionen] leerer Ordner {0} aus einem abgebrochenen Lauf entfernt."),
        ["Diff_LeftoverKept"] = L(
            "[versioni] residuo {0} non rimosso ({1}): resta dov'è, si riprova al prossimo backup.",
            "[versions] leftover {0} not removed ({1}): it stays where it is, to be retried at the next backup.",
            "[versiones] resto {0} no eliminado ({1}): se queda donde está, se reintenta en la próxima copia.",
            "[versions] reste {0} non supprimé ({1}) : il reste en place, nouvelle tentative à la prochaine sauvegarde.",
            "[Versionen] Rest {0} nicht entfernt ({1}): er bleibt, wo er ist, und wird beim nächsten Backup erneut versucht."),
        ["Diff_Removed"] = L(
            "[versioni] rimossa la versione vecchia {0} (ritenzione del job).",
            "[versions] removed old version {0} (the job's retention).",
            "[versiones] eliminada la versión antigua {0} (retención del trabajo).",
            "[versions] ancienne version {0} supprimée (rétention de la tâche).",
            "[Versionen] alte Version {0} entfernt (Aufbewahrung des Jobs)."),
        ["Diff_RemoveFailed"] = L(
            "[versioni] la versione vecchia {0} non si è potuta rimuovere ({1}): il backup è comunque riuscito.",
            "[versions] old version {0} could not be removed ({1}): the backup succeeded anyway.",
            "[versiones] la versión antigua {0} no se ha podido eliminar ({1}): la copia ha funcionado igualmente.",
            "[versions] l'ancienne version {0} n'a pas pu être supprimée ({1}) : la sauvegarde a tout de même réussi.",
            "[Versionen] die alte Version {0} konnte nicht entfernt werden ({1}): das Backup war dennoch erfolgreich."),
        ["Diff_AdoptSkipped"] = L(
            "[versioni] {0} non spostato in «current» ({1}): resta nella destinazione, fuori dalla portata del mirror, e verrà ricopiato dalla sorgente.",
            "[versions] {0} not moved into \"current\" ({1}): it stays in the destination, out of the mirror's reach, and will be recopied from the source.",
            "[versiones] {0} no movido a «current» ({1}): se queda en el destino, fuera del alcance del espejo, y se volverá a copiar desde el origen.",
            "[versions] {0} non déplacé dans « current » ({1}) : il reste dans la destination, hors de portée du miroir, et sera recopié depuis la source.",
            "[Versionen] {0} nicht nach „current“ verschoben ({1}): es bleibt im Ziel, außerhalb der Reichweite der Spiegelung, und wird aus der Quelle neu kopiert."),
        ["Diff_TargetExists"] = L(
            "nella versione c'è già un file con quel nome",
            "the version already holds a file with that name",
            "la versión ya contiene un archivo con ese nombre",
            "la version contient déjà un fichier de ce nom",
            "die Version enthält bereits eine Datei mit diesem Namen"),
        ["Diff_AddedOnly"] = L(
            "[versioni] questo backup ha solo aggiunto file: niente da mettere da parte, quindi nessuna cartella-versione (una vuota occuperebbe un posto nel conto delle versioni da tenere). Resta annotato {0} con l'elenco di ciò che è stato aggiunto.",
            "[versions] this backup only added files: nothing to set aside, so no version folder (an empty one would take up a slot in the count of versions to keep). The note {0} records what was added.",
            "[versiones] esta copia solo ha añadido archivos: nada que apartar, así que ninguna carpeta de versión (una vacía ocuparía un puesto en el recuento de versiones a conservar). Queda anotado {0} con la lista de lo añadido.",
            "[versions] cette sauvegarde n'a fait qu'ajouter des fichiers : rien à mettre de côté, donc aucun dossier de version (un dossier vide occuperait une place dans le nombre de versions à conserver). La note {0} enregistre ce qui a été ajouté.",
            "[Versionen] dieses Backup hat nur Dateien hinzugefügt: nichts beiseitezulegen, also kein Versionsordner (ein leerer würde einen Platz in der Zahl der aufzubewahrenden Versionen belegen). Der Vermerk {0} hält fest, was hinzugekommen ist."),
        ["Diff_SkippedLocked"] = L(
            "[versioni] {0} file non messi da parte perché in uso: lasciati com'erano, si riprova al prossimo backup: {1}",
            "[versions] {0} files not set aside because they are in use: left as they were, to be retried at the next backup: {1}",
            "[versiones] {0} archivos no apartados porque están en uso: se dejan como estaban, se reintenta en la próxima copia: {1}",
            "[versions] {0} fichiers non mis de côté car en cours d'utilisation : laissés tels quels, nouvelle tentative à la prochaine sauvegarde : {1}",
            "[Versionen] {0} Dateien nicht beiseitegelegt, weil sie in Benutzung sind: unverändert gelassen, erneuter Versuch beim nächsten Backup: {1}"),
        ["Diff_AdoptAmbiguous"] = L(
            "[versioni] nella destinazione c'è già una cartella «{0}» e accanto a lei altro contenuto ({1} voci): potrebbe essere la cartella del backup oppure una cartella tua arrivata dalla sorgente. Non viene spostato niente. I nomi «current» e «versions» nella radice di una destinazione con versioni sono riservati a RoboKeep: se sono tuoi, scegli una sottocartella di destinazione diversa.",
            "[versions] the destination already has a \"{0}\" folder with other content beside it ({1} entries): it could be the backup's folder, or a folder of yours that came from the source. Nothing is moved. The names \"current\" and \"versions\" at the root of a versioned destination are reserved by RoboKeep: if they are yours, pick a different destination subfolder.",
            "[versiones] el destino ya tiene una carpeta «{0}» y junto a ella otro contenido ({1} entradas): podría ser la carpeta de la copia o una carpeta tuya llegada desde el origen. No se mueve nada. Los nombres «current» y «versions» en la raíz de un destino con versiones están reservados a RoboKeep: si son tuyos, elige otra subcarpeta de destino.",
            "[versions] la destination contient déjà un dossier « {0} » et, à côté, d'autres éléments ({1} entrées) : ce peut être le dossier de la sauvegarde ou un dossier à vous venu de la source. Rien n'est déplacé. Les noms « current » et « versions » à la racine d'une destination versionnée sont réservés à RoboKeep : s'ils sont à vous, choisissez un autre sous-dossier de destination.",
            "[Versionen] das Ziel enthält bereits einen Ordner „{0}“ und daneben weiteren Inhalt ({1} Einträge): das kann der Ordner des Backups sein oder ein eigener Ordner, der aus der Quelle stammt. Es wird nichts verschoben. Die Namen „current“ und „versions“ im Stammverzeichnis eines versionierten Ziels sind für RoboKeep reserviert: wenn sie Ihnen gehören, wählen Sie einen anderen Zielunterordner."),
        ["Diff_NoChanges"] = L(
            "[versioni] niente di cambiato dall'ultimo backup: la copia in «current» è già aggiornata, nessuna versione nuova.",
            "[versions] nothing changed since the last backup: the copy in \"current\" is already up to date, no new version.",
            "[versiones] ningún cambio desde la última copia: lo que hay en «current» ya está al día, ninguna versión nueva.",
            "[versions] aucun changement depuis la dernière sauvegarde : la copie dans « current » est déjà à jour, pas de nouvelle version.",
            "[Versionen] keine Änderung seit dem letzten Backup: die Kopie in „current“ ist bereits aktuell, keine neue Version."),

        ["Guard_Status"] = L(
            "BLOCCATO: troppe cancellazioni",
            "BLOCKED: too many deletions",
            "BLOQUEADO: demasiadas eliminaciones",
            "BLOQUÉ : trop de suppressions",
            "BLOCKIERT: zu viele Löschungen"),
        ["Guard_Preview"] = L(
            "[controllo] anteprima delle cancellazioni del mirror...",
            "[check] previewing the mirror's deletions...",
            "[control] vista previa de las eliminaciones del espejo...",
            "[contrôle] aperçu des suppressions du miroir...",
            "[Prüfung] Vorschau der Löschungen der Spiegelung..."),
        ["Guard_PreviewFailed"] = L(
            "[controllo] impossibile stimare le cancellazioni (l'anteprima non è riuscita): il mirror parte senza soglia.",
            "[check] cannot estimate the deletions (the preview failed): the mirror runs without the threshold.",
            "[control] no se pueden estimar las eliminaciones (la vista previa no funcionó): el espejo arranca sin umbral.",
            "[contrôle] impossible d'estimer les suppressions (l'aperçu a échoué) : le miroir démarre sans seuil.",
            "[Prüfung] Löschungen nicht abschätzbar (die Vorschau ist fehlgeschlagen): die Spiegelung läuft ohne Schwelle."),
        ["Guard_Blocked"] = L(
            "Mirror fermato: avrebbe cancellato {0:N0} file su {1:N0} ({2} %) in {3}. Se è voluto, avvia il job dalla finestra di RoboKeep e conferma, oppure alza la soglia nell'editor del job.",
            "Mirror stopped: it would have deleted {0:N0} files out of {1:N0} ({2} %) in {3}. If that is what you want, start the job from the RoboKeep window and confirm, or raise the threshold in the job editor.",
            "Espejo detenido: habría eliminado {0:N0} archivos de {1:N0} ({2} %) en {3}. Si es lo que quieres, inicia el trabajo desde la ventana de RoboKeep y confirma, o sube el umbral en el editor del trabajo.",
            "Miroir arrêté : il aurait supprimé {0:N0} fichiers sur {1:N0} ({2} %) dans {3}. Si c'est voulu, lancez la tâche depuis la fenêtre de RoboKeep et confirmez, ou augmentez le seuil dans l'éditeur de la tâche.",
            "Spiegelung gestoppt: sie hätte {0:N0} von {1:N0} Dateien ({2} %) in {3} gelöscht. Wenn das gewollt ist, starten Sie den Job aus dem RoboKeep-Fenster und bestätigen Sie, oder erhöhen Sie die Schwelle im Job-Editor."),
        ["Guard_BlockedVersioned"] = L(
            "Mirror fermato: la nuova versione avrebbe {0:N0} file in meno su {1:N0} ({2} %) rispetto all'ultima ({3}). Se è voluto, avvia il job dalla finestra di RoboKeep e conferma, oppure alza la soglia nell'editor del job.",
            "Mirror stopped: the new version would have {0:N0} files fewer out of {1:N0} ({2} %) than the last one ({3}). If that is what you want, start the job from the RoboKeep window and confirm, or raise the threshold in the job editor.",
            "Espejo detenido: la nueva versión tendría {0:N0} archivos menos de {1:N0} ({2} %) respecto a la última ({3}). Si es lo que quieres, inicia el trabajo desde la ventana de RoboKeep y confirma, o sube el umbral en el editor del trabajo.",
            "Miroir arrêté : la nouvelle version aurait {0:N0} fichiers en moins sur {1:N0} ({2} %) par rapport à la dernière ({3}). Si c'est voulu, lancez la tâche depuis la fenêtre de RoboKeep et confirmez, ou augmentez le seuil dans l'éditeur de la tâche.",
            "Spiegelung gestoppt: die neue Version hätte {0:N0} von {1:N0} Dateien ({2} %) weniger als die letzte ({3}). Wenn das gewollt ist, starten Sie den Job aus dem RoboKeep-Fenster und bestätigen Sie, oder erhöhen Sie die Schwelle im Job-Editor."),
        ["Guard_DryRunNote"] = L(
            "Il mirror cancellerebbe {0:N0} file su {1:N0} ({2} %): sopra la soglia del {3} % del job.",
            "The mirror would delete {0:N0} files out of {1:N0} ({2} %): above the job's {3} % threshold.",
            "El espejo eliminaría {0:N0} archivos de {1:N0} ({2} %): por encima del umbral del {3} % del trabajo.",
            "Le miroir supprimerait {0:N0} fichiers sur {1:N0} ({2} %) : au-dessus du seuil de {3} % de la tâche.",
            "Die Spiegelung würde {0:N0} von {1:N0} Dateien ({2} %) löschen: über der Schwelle von {3} % des Jobs."),
        ["Guard_EmailSubject"] = L(
            "BLOCCATO: troppe cancellazioni",
            "BLOCKED: too many deletions",
            "BLOQUEADO: demasiadas eliminaciones",
            "BLOQUÉ : trop de suppressions",
            "BLOCKIERT: zu viele Löschungen"),

        ["Space_Status"] = L(
            "Disco pieno", "Disk full", "Disco lleno", "Disque plein", "Datenträger voll"),
        ["Space_Detail"] = L(
            "{0} pieno: il backup non è stato completato. Le {1} versioni del job occupano {2}. Abbassa «Numero massimo di versioni» nell'editor del job — il run successivo cancella le più vecchie — oppure attiva «Quando il disco di backup è pieno, cancella le versioni più vecchie» in Impostazioni → Affidabilità. Per fare posto subito: Versioni... → Apri in Esplora risorse e cancella a mano qualche cartella datata.",
            "{0} is full: the backup was not completed. The job's {1} versions take up {2}. Lower \"Max number of versions\" in the job editor — the next run deletes the oldest ones — or turn on \"When the backup disk is full, delete the oldest versions\" in Settings → Reliability. To make room right now: Versions... → Open in File Explorer and delete a few dated folders by hand.",
            "{0} lleno: la copia no se ha completado. Las {1} versiones del trabajo ocupan {2}. Baja «Número máximo de versiones» en el editor del trabajo (la siguiente ejecución elimina las más antiguas) o activa «Cuando el disco de backup está lleno, elimina las versiones más antiguas» en Ajustes → Fiabilidad. Para hacer sitio ahora mismo: Versiones... → Abrir en el Explorador y elimina a mano alguna carpeta con fecha.",
            "{0} plein : la sauvegarde n'a pas été terminée. Les {1} versions de la tâche occupent {2}. Réduisez « Nombre max de versions » dans l'éditeur de la tâche — la prochaine exécution supprime les plus anciennes — ou activez « Quand le disque de sauvegarde est plein, supprimer les versions les plus anciennes » dans Paramètres → Fiabilité. Pour faire de la place tout de suite : Versions... → Ouvrir dans l'Explorateur et supprimez à la main quelques dossiers datés.",
            "{0} ist voll: das Backup wurde nicht abgeschlossen. Die {1} Versionen des Jobs belegen {2}. Verringern Sie „Maximale Anzahl Versionen“ im Job-Editor — der nächste Lauf löscht die ältesten — oder aktivieren Sie „Wenn der Backup-Datenträger voll ist, die ältesten Versionen löschen“ in Einstellungen → Zuverlässigkeit. Um sofort Platz zu schaffen: Versionen... → Im Explorer öffnen und einige datierte Ordner von Hand löschen."),
        ["Space_DetailNoVersions"] = L(
            "{0} pieno: il backup non è stato completato. Fai posto sul disco o scegli una destinazione più capiente; questo job non tiene versioni, quindi non c'è niente da cancellare qui.",
            "{0} is full: the backup was not completed. Free up space on the disk or pick a roomier destination; this job keeps no versions, so there is nothing to delete here.",
            "{0} lleno: la copia no se ha completado. Libera espacio en el disco o elige un destino más amplio; este trabajo no conserva versiones, así que aquí no hay nada que eliminar.",
            "{0} plein : la sauvegarde n'a pas été terminée. Libérez de l'espace sur le disque ou choisissez une destination plus grande ; cette tâche ne conserve pas de versions, il n'y a donc rien à supprimer ici.",
            "{0} ist voll: das Backup wurde nicht abgeschlossen. Schaffen Sie Platz auf dem Datenträger oder wählen Sie ein größeres Ziel; dieser Job behält keine Versionen, hier gibt es also nichts zu löschen."),
        ["Space_Freed"] = L(
            "[spazio] liberati {0} cancellando la versione {1}",
            "[space] freed {0} by deleting version {1}",
            "[espacio] liberados {0} eliminando la versión {1}",
            "[espace] {0} libérés en supprimant la version {1}",
            "[Speicher] {0} freigegeben durch Löschen der Version {1}"),
        ["Space_FreedNothing"] = L(
            "[spazio] la cancellazione della versione {0} non ha liberato spazio misurabile (era tutta condivisa con le altre versioni).",
            "[space] deleting version {0} freed no measurable space (it was entirely shared with the other versions).",
            "[espacio] eliminar la versión {0} no ha liberado espacio apreciable (estaba toda compartida con las demás versiones).",
            "[espace] la suppression de la version {0} n'a libéré aucun espace mesurable (elle était entièrement partagée avec les autres versions).",
            "[Speicher] das Löschen der Version {0} hat keinen messbaren Speicher freigegeben (sie war vollständig mit den anderen Versionen geteilt)."),
        ["Space_NotFreed"] = L(
            "[spazio] versione {0} non cancellata ({1}): la pulizia si ferma qui.",
            "[space] version {0} not deleted ({1}): the cleanup stops here.",
            "[espacio] versión {0} no eliminada ({1}): la limpieza se detiene aquí.",
            "[espace] version {0} non supprimée ({1}) : le nettoyage s'arrête ici.",
            "[Speicher] Version {0} nicht gelöscht ({1}): die Bereinigung endet hier."),
        ["Space_EmailSubject"] = L(
            "DISCO PIENO: {0}", "DISK FULL: {0}", "DISCO LLENO: {0}",
            "DISQUE PLEIN : {0}", "DATENTRÄGER VOLL: {0}"),
        ["Space_Unknown"] = L("n/d", "n/a", "n/d", "n/d", "k. A."),

        ["ConfigCopy_Written"] = L(
            "[config] copia della configurazione salvata in {0} (job, esclusioni e impostazioni, senza password).",
            "[config] configuration copy saved to {0} (jobs, exclusions and settings, without passwords).",
            "[config] copia de la configuración guardada en {0} (trabajos, exclusiones y ajustes, sin contraseñas).",
            "[config] copie de la configuration enregistrée dans {0} (tâches, exclusions et paramètres, sans mots de passe).",
            "[config] Konfigurationskopie in {0} gespeichert (Jobs, Ausschlüsse und Einstellungen, ohne Passwörter)."),
        ["ConfigCopy_Failed"] = L(
            "[config] copia della configurazione non salvata ({0}): il backup dei file è comunque riuscito.",
            "[config] configuration copy not saved ({0}): the file backup succeeded anyway.",
            "[config] copia de la configuración no guardada ({0}): la copia de los archivos ha funcionado igualmente.",
            "[config] copie de la configuration non enregistrée ({0}) : la sauvegarde des fichiers a tout de même réussi.",
            "[config] Konfigurationskopie nicht gespeichert ({0}): das Datei-Backup war dennoch erfolgreich."),

        ["Health_Detail"] = L(
            "{0} blocchi danneggiati, {1} errori di I/O, {2} errori del file system (ultimo: {3})",
            "{0} bad blocks, {1} I/O errors, {2} file system errors (latest: {3})",
            "{0} bloques dañados, {1} errores de E/S, {2} errores del sistema de archivos (último: {3})",
            "{0} blocs défectueux, {1} erreurs d'E/S, {2} erreurs du système de fichiers (dernière : {3})",
            "{0} defekte Blöcke, {1} E/A-Fehler, {2} Dateisystemfehler (zuletzt: {3})"),
        ["Health_Warning"] = L(
            "[disco] ATTENZIONE: negli ultimi giorni Windows ha registrato errori sul disco {0} (o su un altro disco che ha usato la stessa lettera): {1}. Controlla cavo, box e alimentazione, poi la salute del disco (SMART), prima che il problema diventi un danno.",
            "[disk] WARNING: in the last few days Windows logged errors on disk {0} (or on another disk that used the same letter): {1}. Check cable, enclosure and power, then the disk's health (SMART), before the problem turns into damage.",
            "[disco] ATENCIÓN: en los últimos días Windows registró errores en el disco {0} (o en otro disco que usó la misma letra): {1}. Comprueba cable, caja y alimentación, y luego la salud del disco (SMART), antes de que el problema se convierta en daño.",
            "[disque] ATTENTION : ces derniers jours, Windows a enregistré des erreurs sur le disque {0} (ou sur un autre disque ayant utilisé la même lettre) : {1}. Vérifiez câble, boîtier et alimentation, puis l'état du disque (SMART), avant que le problème ne devienne un dégât.",
            "[Datenträger] ACHTUNG: in den letzten Tagen hat Windows Fehler auf Datenträger {0} protokolliert (oder auf einem anderen Datenträger mit demselben Buchstaben): {1}. Kabel, Gehäuse und Stromversorgung prüfen, dann den Zustand des Datenträgers (SMART), bevor aus dem Problem ein Schaden wird."),

        ["Hw_Stop"] = L(
            "[disco] ERRORE HARDWARE — job INTERROTTO per non peggiorare il danno: {0}",
            "[disk] HARDWARE ERROR — job STOPPED to avoid making the damage worse: {0}",
            "[disco] ERROR DE HARDWARE — trabajo DETENIDO para no agravar el daño: {0}",
            "[disque] ERREUR MATÉRIELLE — tâche ARRÊTÉE pour ne pas aggraver les dégâts : {0}",
            "[Datenträger] HARDWAREFEHLER — Job GESTOPPT, um den Schaden nicht zu vergrößern: {0}"),
        ["Hw_Advice"] = L(
            "[disco] Un disco (sorgente o destinazione) o il suo collegamento ha segnalato un errore fisico. Non rilanciare il backup a ripetizione. Controlla: 1) cavo, box USB e alimentazione (prova un altro cavo/porta); 2) la salute del disco (SMART, es. CrystalDiskInfo: voci 05, C5, C6 = disco; C7 = collegamento tra box e disco — un cavo USB difettoso invece non lascia traccia nello SMART). Se il disco sta cedendo, metti in salvo i dati prima di ogni altra cosa.",
            "[disk] A disk (source or destination) or its connection reported a physical error. Do not keep re-running the backup. Check: 1) cable, USB enclosure and power (try another cable/port); 2) the disk's health (SMART, e.g. CrystalDiskInfo: attributes 05, C5, C6 = disk; C7 = the link between enclosure and disk — a faulty USB cable, by contrast, leaves no trace in SMART). If the disk is failing, rescue your data before anything else.",
            "[disco] Un disco (origen o destino) o su conexión ha notificado un error físico. No relances la copia repetidamente. Comprueba: 1) cable, caja USB y alimentación (prueba otro cable/puerto); 2) la salud del disco (SMART, p. ej. CrystalDiskInfo: atributos 05, C5, C6 = disco; C7 = enlace entre la caja y el disco; un cable USB defectuoso, en cambio, no deja rastro en SMART). Si el disco está fallando, pon a salvo los datos antes que nada.",
            "[disque] Un disque (source ou destination) ou sa connexion a signalé une erreur physique. Ne relancez pas la sauvegarde en boucle. Vérifiez : 1) câble, boîtier USB et alimentation (essayez un autre câble/port) ; 2) l'état du disque (SMART, p. ex. CrystalDiskInfo : attributs 05, C5, C6 = disque ; C7 = liaison entre le boîtier et le disque — un câble USB défectueux, lui, ne laisse aucune trace dans SMART). Si le disque est en train de lâcher, mettez vos données à l'abri avant tout.",
            "[Datenträger] Ein Datenträger (Quelle oder Ziel) oder seine Verbindung hat einen physischen Fehler gemeldet. Das Backup nicht wiederholt neu starten. Prüfen: 1) Kabel, USB-Gehäuse und Stromversorgung (anderes Kabel/anderen Port testen); 2) den Zustand des Datenträgers (SMART, z. B. CrystalDiskInfo: Attribute 05, C5, C6 = Datenträger; C7 = Verbindung zwischen Gehäuse und Datenträger — ein defektes USB-Kabel hinterlässt dagegen keine Spur in SMART). Wenn der Datenträger ausfällt, zuerst die Daten retten."),
        ["Hw_Status"] = L(
            "INTERROTTO: errore hardware del disco o del collegamento.",
            "STOPPED: hardware error from the disk or its connection.",
            "DETENIDO: error de hardware del disco o de su conexión.",
            "ARRÊTÉ : erreur matérielle du disque ou de sa connexion.",
            "GESTOPPT: Hardwarefehler des Datenträgers oder seiner Verbindung."),
        ["Hw_NotStartedStatus"] = L(
            "Non eseguito: il disco è a riposo dopo un errore hardware.",
            "Not run: the disk is resting after a hardware error.",
            "No ejecutado: el disco está en reposo tras un error de hardware.",
            "Non exécutée : le disque est au repos après une erreur matérielle.",
            "Nicht ausgeführt: der Datenträger ruht nach einem Hardwarefehler."),
        ["Email_HardwareError"] = L("ERRORE HARDWARE", "HARDWARE ERROR", "ERROR DE HARDWARE",
            "ERREUR MATÉRIELLE", "HARDWAREFEHLER"),
        ["Hw_SkippedAfterFault"] = L(
            "[disco] job NON eseguito: il disco {0} ha segnalato un errore hardware in un job precedente di questa sessione.",
            "[disk] job NOT run: disk {0} reported a hardware error in an earlier job of this session.",
            "[disco] trabajo NO ejecutado: el disco {0} notificó un error de hardware en un trabajo anterior de esta sesión.",
            "[disque] tâche NON exécutée : le disque {0} a signalé une erreur matérielle lors d'une tâche précédente de cette session.",
            "[Datenträger] Job NICHT ausgeführt: Datenträger {0} hat in einem früheren Job dieser Sitzung einen Hardwarefehler gemeldet."),
        ["Hw_SkippedPersisted"] = L(
            "[disco] job NON eseguito: il disco {0} è a riposo dal {1} per un errore hardware. Controllalo, poi premi «Riattiva dischi» nella finestra principale (si riattiva da solo dopo {2} giorni).",
            "[disk] job NOT run: disk {0} has been resting since {1} after a hardware error. Check it, then click \"Re-enable disks\" in the main window (it re-enables itself after {2} days).",
            "[disco] trabajo NO ejecutado: el disco {0} está en reposo desde el {1} por un error de hardware. Compruébalo y pulsa «Rehabilitar discos» en la ventana principal (se rehabilita solo tras {2} días).",
            "[disque] tâche NON exécutée : le disque {0} est au repos depuis le {1} après une erreur matérielle. Vérifiez-le, puis cliquez sur « Réactiver les disques » dans la fenêtre principale (réactivation automatique après {2} jours).",
            "[Datenträger] Job NICHT ausgeführt: Datenträger {0} ruht seit {1} nach einem Hardwarefehler. Prüfen Sie ihn und klicken Sie im Hauptfenster auf „Datenträger freigeben“ (nach {2} Tagen automatisch)."),
        ["Hw_VerifyStop"] = L(
            "[verifica] INTERROTTA per errore hardware: {0}",
            "[verify] STOPPED due to a hardware error: {0}",
            "[verificación] DETENIDA por error de hardware: {0}",
            "[vérification] ARRÊTÉE pour erreur matérielle : {0}",
            "[Prüfung] GESTOPPT wegen Hardwarefehler: {0}"),

        ["Threads_Capped"] = L(
            "[disco] {0} è un disco meccanico (o un disco USB non identificabile): thread di copia limitati da {1} a {2} per non stressare la testina.",
            "[disk] {0} is a mechanical disk (or an unidentifiable USB disk): copy threads limited from {1} to {2} to avoid thrashing the head.",
            "[disco] {0} es un disco mecánico (o un disco USB no identificable): hilos de copia limitados de {1} a {2} para no forzar el cabezal.",
            "[disque] {0} est un disque mécanique (ou un disque USB non identifiable) : threads de copie limités de {1} à {2} pour ménager la tête de lecture.",
            "[Datenträger] {0} ist eine mechanische Festplatte (oder ein nicht identifizierbarer USB-Datenträger): Kopier-Threads von {1} auf {2} begrenzt, um den Kopf zu schonen."),
        ["Email_SendFailed"] = L(
            "[email] invio non riuscito: {0}",
            "[email] send failed: {0}",
            "[email] envío fallido: {0}",
            "[email] échec de l'envoi : {0}",
            "[E-Mail] Senden fehlgeschlagen: {0}"),
        ["Verify_Failed"] = L(
            "[verifica] non riuscita: {0}",
            "[verify] failed: {0}",
            "[verificación] fallida: {0}",
            "[vérification] échouée : {0}",
            "[Prüfung] fehlgeschlagen: {0}"),

        ["Lbl_Result"] = L("Esito", "Result", "Resultado", "Résultat", "Ergebnis"),
        ["Lbl_Error"] = L("ERRORE", "ERROR", "ERROR", "ERREUR", "FEHLER"),
        ["Lbl_FoldersCopied"] = L("Cartelle copiate", "Folders copied", "Carpetas copiadas", "Dossiers copiés", "Ordner kopiert"),
        ["Lbl_FilesCopied"] = L("File copiati", "Files copied", "Archivos copiados", "Fichiers copiés", "Dateien kopiert"),
        ["Lbl_FilesUnchanged"] = L("File invariati", "Files unchanged", "Archivos sin cambios", "Fichiers inchangés", "Dateien unverändert"),
        ["Lbl_FilesExtra"] = L("File extra", "Extra files", "Archivos extra", "Fichiers en trop", "Zusätzliche Dateien"),
        ["Lbl_FilesFailed"] = L("File falliti", "Files failed", "Archivos fallidos", "Fichiers en échec", "Fehlgeschlagene Dateien"),
        ["Lbl_DirsFailed"] = L("Cartelle fallite", "Folders failed", "Carpetas fallidas", "Dossiers en échec", "Fehlgeschlagene Ordner"),
        ["Lbl_FoldersExtra"] = L("Cartelle extra", "Extra folders", "Carpetas extra", "Dossiers en trop", "Zusätzliche Ordner"),
        ["Lbl_Duration"] = L("Durata", "Duration", "Duración", "Durée", "Dauer"),

        ["Volume_Skipped"] = L(
            "[disco] job saltato: scrive sul disco {0}, ma il disco collegato è {1}.",
            "[disk] job skipped: it writes to disk {0}, but the connected disk is {1}.",
            "[disco] trabajo omitido: escribe en el disco {0}, pero el disco conectado es {1}.",
            "[disque] tâche ignorée : elle écrit sur le disque {0}, mais le disque connecté est {1}.",
            "[Datenträger] Job übersprungen: schreibt auf {0}, angeschlossen ist aber {1}."),
        ["Volume_SkippedAbsent"] = L(
            "[disco] job saltato: questo job scrive sul disco {0}, che non risulta collegato.",
            "[disk] job skipped: this job writes to disk {0}, which is not connected.",
            "[disco] trabajo omitido: este trabajo escribe en el disco {0}, que no está conectado.",
            "[disque] tâche ignorée : cette tâche écrit sur le disque {0}, qui n'est pas connecté.",
            "[Datenträger] Job übersprungen: dieser Job schreibt auf {0}, der nicht angeschlossen ist."),
        ["Volume_SkippedStatus"] = L(
            "Saltato: il disco atteso non è collegato.",
            "Skipped: the expected disk is not connected.",
            "Omitido: el disco esperado no está conectado.",
            "Ignoré : le disque attendu n'est pas connecté.",
            "Übersprungen: der erwartete Datenträger ist nicht angeschlossen."),
        ["Volume_Unknown"] = L(
            "sconosciuto", "unknown", "desconocido", "inconnu", "unbekannt"),

        ["Email_Preview"] = L("Anteprima", "Preview", "Vista previa", "Aperçu", "Vorschau"),
        ["Email_Yes"] = L("sì", "yes", "sí", "oui", "ja"),
        ["Email_No"] = L("no", "no", "no", "non", "nein"),
        ["Email_Start"] = L("Inizio", "Start", "Inicio", "Début", "Start"),

        ["Test_Subject"] = L("Email di prova", "Test email", "Correo de prueba", "E-mail de test", "Test-E-Mail"),
        ["Test_Body"] = L(
            "Questa è un'email di prova inviata da RoboKeep. Se la ricevi, la configurazione SMTP funziona.",
            "This is a test email sent by RoboKeep. If you receive it, your SMTP configuration works.",
            "Este es un correo de prueba enviado por RoboKeep. Si lo recibes, tu configuración SMTP funciona.",
            "Ceci est un e-mail de test envoyé par RoboKeep. Si vous le recevez, votre configuration SMTP fonctionne.",
            "Dies ist eine von RoboKeep gesendete Test-E-Mail. Wenn du sie erhältst, funktioniert deine SMTP-Konfiguration."),
    };
}
