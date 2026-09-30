using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep;

public partial class SnapshotsWindow : Wpf.Ui.Controls.FluentWindow
{
    // La cartella che contiene le versioni: la destinazione stessa nel modello a hard-link,
    // «versions» in quello per differenza.
    private readonly string _versionsFolder;
    private readonly BackupJob _job;

    public SnapshotsWindow(BackupJob job)
    {
        _job = job;
        var destination = job.Destination ?? "";
        // Quale layout ha questa destinazione si legge dalle cartelle che ci sono, senza mai
        // scrivere il file di prova degli hard-link: questa finestra guarda e apre, non decide.
        IsDifferential = VersioningLayout.DetectReadOnly(destination) == VersioningMode.Differential;
        _versionsFolder = IsDifferential ? VersioningLayout.VersionsDir(destination) : destination;
        Snapshots = SnapshotName.ListValid(_versionsFolder).ToList();

        InitializeComponent();
        DataContext = this;
        Title = job.Name;

        // Preseleziona il più recente (lista ordinata dal più nuovo): il pulsante apre sempre
        // qualcosa di VISIBILMENTE selezionato, senza fallback nascosti.
        if (Snapshots.Count > 0)
            SnapshotList.SelectedIndex = 0;
    }

    public List<string> Snapshots { get; }
    public bool HasSnapshots => Snapshots.Count > 0;
    public bool IsEmpty => Snapshots.Count == 0;

    /// <summary>true se il job usa le versioni per differenza: una cartella-data non è l'albero
    /// intero di quel giorno ma i soli file che quel backup ha sostituito o cancellato, e va detto
    /// prima che l'utente ci guardi dentro e si spaventi di non trovarci tutto.</summary>
    public bool IsDifferential { get; }

    private void OpenSelected()
    {
        // Apri solo ciò che è selezionato: niente fallback nascosto (il più recente è già preselezionato).
        if (SnapshotList.SelectedItem is not string snap) return;

        // Percorso tra virgolette: senza, una destinazione con uno spazio nel nome
        // («D:\I miei backup\…») arriverebbe a Esplora risorse spezzata in due argomenti e si
        // aprirebbe la cartella Documenti invece della versione.
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = "\"" + Path.Combine(_versionsFolder, snap) + "\"",
            UseShellExecute = true,
        });
    }

    private void OnSnapshotDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => OpenSelected();

    private void OnOpenInExplorer(object sender, RoutedEventArgs e)
        => OpenSelected();

    /// <summary>Apre il ripristino guidato sulla versione selezionata: qui si guarda, di là si
    /// rimette a posto. La data scelta arriva già impostata, altrimenti l'utente dovrebbe
    /// ritrovarla in un secondo elenco.</summary>
    private void OnRestore(object sender, RoutedEventArgs e)
    {
        var chosen = SnapshotList.SelectedItem as string;
        new RestoreWindow(_job, chosen) { Owner = this }.ShowDialog();
    }

    private void OnClose(object sender, RoutedEventArgs e)
        => Close();
}
