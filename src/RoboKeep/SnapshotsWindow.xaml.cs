using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using RoboKeep.Core.Services;

namespace RoboKeep;

public partial class SnapshotsWindow : Wpf.Ui.Controls.FluentWindow
{
    // La cartella che contiene le versioni: la destinazione stessa nel modello a hard-link,
    // «versions» in quello per differenza.
    private readonly string _versionsFolder;

    public SnapshotsWindow(string jobName, string destination)
    {
        // Quale layout ha questa destinazione si legge dalle cartelle che ci sono, senza mai
        // scrivere il file di prova degli hard-link: questa finestra guarda e apre, non decide.
        var differentialVersions = VersioningLayout.VersionsDir(destination);
        IsDifferential = SnapshotName.ListValid(destination).Count == 0
            && Directory.Exists(differentialVersions);
        _versionsFolder = IsDifferential ? differentialVersions : destination;
        Snapshots = SnapshotName.ListValid(_versionsFolder).ToList();

        InitializeComponent();
        DataContext = this;
        Title = jobName;

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

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = Path.Combine(_versionsFolder, snap),
            UseShellExecute = true,
        });
    }

    private void OnSnapshotDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => OpenSelected();

    private void OnOpenInExplorer(object sender, RoutedEventArgs e)
        => OpenSelected();

    private void OnClose(object sender, RoutedEventArgs e)
        => Close();
}
