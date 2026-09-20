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

        ["Hw_Stop"] = L(
            "[disco] ERRORE HARDWARE — job INTERROTTO per non peggiorare il danno: {0}",
            "[disk] HARDWARE ERROR — job STOPPED to avoid making the damage worse: {0}",
            "[disco] ERROR DE HARDWARE — trabajo DETENIDO para no agravar el daño: {0}",
            "[disque] ERREUR MATÉRIELLE — tâche ARRÊTÉE pour ne pas aggraver les dégâts : {0}",
            "[Datenträger] HARDWAREFEHLER — Job GESTOPPT, um den Schaden nicht zu vergrößern: {0}"),
        ["Hw_Advice"] = L(
            "[disco] Un disco (sorgente o destinazione) o il suo collegamento ha segnalato un errore fisico. Non rilanciare il backup a ripetizione. Controlla: 1) cavo, box USB e alimentazione (prova un altro cavo/porta); 2) la salute del disco (SMART, es. CrystalDiskInfo: voci 05, C5, C6 = disco; C7 = cavo). Se il disco sta cedendo, metti in salvo i dati prima di ogni altra cosa.",
            "[disk] A disk (source or destination) or its connection reported a physical error. Do not keep re-running the backup. Check: 1) cable, USB enclosure and power (try another cable/port); 2) the disk's health (SMART, e.g. CrystalDiskInfo: attributes 05, C5, C6 = disk; C7 = cable). If the disk is failing, rescue your data before anything else.",
            "[disco] Un disco (origen o destino) o su conexión ha notificado un error físico. No relances la copia repetidamente. Comprueba: 1) cable, caja USB y alimentación (prueba otro cable/puerto); 2) la salud del disco (SMART, p. ej. CrystalDiskInfo: atributos 05, C5, C6 = disco; C7 = cable). Si el disco está fallando, pon a salvo los datos antes que nada.",
            "[disque] Un disque (source ou destination) ou sa connexion a signalé une erreur physique. Ne relancez pas la sauvegarde en boucle. Vérifiez : 1) câble, boîtier USB et alimentation (essayez un autre câble/port) ; 2) l'état du disque (SMART, p. ex. CrystalDiskInfo : attributs 05, C5, C6 = disque ; C7 = câble). Si le disque est en train de lâcher, mettez vos données à l'abri avant tout.",
            "[Datenträger] Ein Datenträger (Quelle oder Ziel) oder seine Verbindung hat einen physischen Fehler gemeldet. Das Backup nicht wiederholt neu starten. Prüfen: 1) Kabel, USB-Gehäuse und Stromversorgung (anderes Kabel/anderen Port testen); 2) den Zustand des Datenträgers (SMART, z. B. CrystalDiskInfo: Attribute 05, C5, C6 = Datenträger; C7 = Kabel). Wenn der Datenträger ausfällt, zuerst die Daten retten."),
        ["Hw_Status"] = L(
            "INTERROTTO: errore hardware del disco o del collegamento.",
            "STOPPED: hardware error from the disk or its connection.",
            "DETENIDO: error de hardware del disco o de su conexión.",
            "ARRÊTÉ : erreur matérielle du disque ou de sa connexion.",
            "GESTOPPT: Hardwarefehler des Datenträgers oder seiner Verbindung."),
        ["Hw_SkippedAfterFault"] = L(
            "[disco] job NON eseguito: il disco {0} ha segnalato un errore hardware in un job precedente di questa sessione.",
            "[disk] job NOT run: disk {0} reported a hardware error in an earlier job of this session.",
            "[disco] trabajo NO ejecutado: el disco {0} notificó un error de hardware en un trabajo anterior de esta sesión.",
            "[disque] tâche NON exécutée : le disque {0} a signalé une erreur matérielle lors d'une tâche précédente de cette session.",
            "[Datenträger] Job NICHT ausgeführt: Datenträger {0} hat in einem früheren Job dieser Sitzung einen Hardwarefehler gemeldet."),
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
