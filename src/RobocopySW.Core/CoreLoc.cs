using System.Globalization;

namespace RobocopySW.Core;

/// <summary>
/// Localizzazione dei testi generati da Core (esiti, riepiloghi, email) in 5 lingue,
/// basata sulla cultura UI corrente del thread (impostata dall'app). Fallback all'inglese.
/// </summary>
internal static class CoreLoc
{
    private static string Lang => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
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
            "RIEPILOGO ANTEPRIMA (RobocopySW)", "PREVIEW SUMMARY (RobocopySW)",
            "RESUMEN VISTA PREVIA (RobocopySW)", "RÉSUMÉ APERÇU (RobocopySW)",
            "VORSCHAU-ZUSAMMENFASSUNG (RobocopySW)"),
        ["Recap_Title"] = L(
            "RIEPILOGO (RobocopySW)", "SUMMARY (RobocopySW)",
            "RESUMEN (RobocopySW)", "RÉSUMÉ (RobocopySW)", "ZUSAMMENFASSUNG (RobocopySW)"),
        ["Recap_ExtraDry"] = L(
            "  (verrebbero rimossi in mirror)", "  (would be removed in mirror)",
            "  (se eliminarían en modo espejo)", "  (seraient supprimés en miroir)",
            "  (würden im Spiegelmodus entfernt)"),
        ["Recap_ExtraReal"] = L(
            "  (in dest, non in sorgente)", "  (in dest, not in source)",
            "  (en destino, no en origen)", "  (dans la destination, pas dans la source)",
            "  (im Ziel, nicht in der Quelle)"),

        ["Lbl_Result"] = L("Esito", "Result", "Resultado", "Résultat", "Ergebnis"),
        ["Lbl_Error"] = L("ERRORE", "ERROR", "ERROR", "ERREUR", "FEHLER"),
        ["Lbl_FoldersCopied"] = L("Cartelle copiate", "Folders copied", "Carpetas copiadas", "Dossiers copiés", "Ordner kopiert"),
        ["Lbl_FilesCopied"] = L("File copiati", "Files copied", "Archivos copiados", "Fichiers copiés", "Dateien kopiert"),
        ["Lbl_FilesUnchanged"] = L("File invariati", "Files unchanged", "Archivos sin cambios", "Fichiers inchangés", "Dateien unverändert"),
        ["Lbl_FilesExtra"] = L("File extra", "Extra files", "Archivos extra", "Fichiers en trop", "Zusätzliche Dateien"),
        ["Lbl_FilesFailed"] = L("File falliti", "Files failed", "Archivos fallidos", "Fichiers en échec", "Fehlgeschlagene Dateien"),
        ["Lbl_Duration"] = L("Durata", "Duration", "Duración", "Durée", "Dauer"),

        ["Email_Preview"] = L("Anteprima", "Preview", "Vista previa", "Aperçu", "Vorschau"),
        ["Email_Yes"] = L("sì", "yes", "sí", "oui", "ja"),
        ["Email_No"] = L("no", "no", "no", "non", "nein"),
        ["Email_Start"] = L("Inizio", "Start", "Inicio", "Début", "Start"),
    };
}
