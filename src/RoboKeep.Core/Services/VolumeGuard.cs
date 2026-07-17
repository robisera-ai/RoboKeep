namespace RoboKeep.Core.Services;

/// <summary>Esito del controllo del disco di destinazione.</summary>
public enum VolumeCheck
{
    /// <summary>Il disco collegato è quello atteso: si può eseguire.</summary>
    Ok,
    /// <summary>Nessun controllo possibile o richiesto: si esegue come sempre.</summary>
    NoExpectation,
    /// <summary>Il disco collegato NON è quello per cui il job è stato configurato.</summary>
    WrongDisk,
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
        // Volume non determinabile (rete, disco assente): la raggiungibilita' e' gia' compito
        // dei controlli pre-avvio, qui non si blocca nulla.
        if (current is null) return VolumeCheck.NoExpectation;

        // Solo l'identificativo decide: le etichette sono modificabili e duplicabili.
        return string.Equals(expectedId, current.VolumeId, StringComparison.OrdinalIgnoreCase)
            ? VolumeCheck.Ok
            : VolumeCheck.WrongDisk;
    }
}
