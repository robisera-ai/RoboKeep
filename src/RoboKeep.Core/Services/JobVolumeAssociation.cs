using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>
/// Scrive l'associazione di un job al disco di destinazione. Punto UNICO in cui
/// <see cref="BackupJob.DestinationVolumeId"/> viene assegnato, condiviso tra l'editor
/// (pulsante "Usa questo disco" e cambio destinazione) e la creazione di un job nuovo.
/// Averlo qui evita che i due percorsi scrivano l'associazione in modi divergenti.
/// </summary>
public static class JobVolumeAssociation
{
    /// <summary>Associa il job al volume attualmente presente alla sua destinazione. Se la
    /// destinazione non è identificabile (share di rete o disco non collegato) l'associazione
    /// viene azzerata: meglio nessun controllo che un id stantìo, che il runner scambierebbe
    /// per il disco atteso.</summary>
    public static void ToCurrentDisk(BackupJob job)
    {
        var current = VolumeIdentity.ForPath(job.Destination);
        job.DestinationVolumeId = current?.VolumeId;
        job.DestinationVolumeLabel = current?.Label;
    }
}
