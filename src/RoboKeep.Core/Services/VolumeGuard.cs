namespace RoboKeep.Core.Services;

/// <summary>Esito del controllo del disco di destinazione.</summary>
public enum VolumeCheck
{
    /// <summary>Nessun controllo possibile o richiesto: si esegue come sempre.
    /// È il valore 0 di proposito: se un domani qualcuno lasciasse un <c>VolumeCheck</c>
    /// al default, deve ottenere "non ho verificato nulla", non un permesso a procedere.</summary>
    NoExpectation,
    /// <summary>Il disco collegato è quello atteso: si può eseguire.</summary>
    Ok,
    /// <summary>Il disco collegato NON è quello per cui il job è stato configurato.</summary>
    WrongDisk,
    /// <summary>Il job attende un disco preciso ma il volume alla destinazione non è
    /// identificabile: nessun disco collegato, lettera inesistente, o errore di lettura.
    /// Distinto da WrongDisk solo per poterlo raccontare all'utente con parole giuste:
    /// per la decisione i due casi sono lo stesso, non si tocca nulla.</summary>
    DiskAbsent,
}

/// <summary>
/// Decide se un job può girare sul disco attualmente collegato (funzione pura).
/// La protezione della rotazione dei dischi vive qui: è la decisione che impedisce a un
/// mirror di cancellare gli snapshot del disco sbagliato, quindi è testata al 100%.
/// </summary>
public static class VolumeGuard
{
    /// <summary>Confronta il volume atteso dal job con quello effettivamente presente.</summary>
    public static VolumeCheck Check(string? expectedId, VolumeInfo? current)
    {
        // Nessuna aspettativa registrata (job creato prima della v1.5, o destinazione di rete).
        if (string.IsNullOrWhiteSpace(expectedId)) return VolumeCheck.NoExpectation;
        // Il job attende un disco preciso ma non c'e' niente di identificabile alla
        // destinazione: lasciar partire robocopy verso una lettera vuota significa un errore
        // rosso ogni notte, quindi si salta come col disco sbagliato.
        if (current is null) return VolumeCheck.DiskAbsent;

        // Solo l'identificativo decide: le etichette sono modificabili e duplicabili.
        return string.Equals(expectedId, current.VolumeId, StringComparison.OrdinalIgnoreCase)
            ? VolumeCheck.Ok
            : VolumeCheck.WrongDisk;
    }

    /// <summary>true se il disco atteso non c'è: il job non deve toccare la destinazione.
    /// Unico punto in cui si decide quali esiti sono "disco non disponibile", così i
    /// chiamanti non replicano la lista degli stati (e non la lasciano divergere).</summary>
    public static bool IsAway(VolumeCheck check) => check is VolumeCheck.WrongDisk or VolumeCheck.DiskAbsent;
}
