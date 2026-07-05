using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using RoboKeep.Core.Services;

namespace RoboKeep;

public partial class SnapshotsWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly string _destination;

    public SnapshotsWindow(string jobName, string destination)
    {
        _destination = destination;
        Snapshots = LoadSnapshots(destination);
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

    private static List<string> LoadSnapshots(string dest)
        => SnapshotName.ListValid(dest).ToList();

    private void OpenSelected()
    {
        // Apri solo ciò che è selezionato: niente fallback nascosto (il più recente è già preselezionato).
        if (SnapshotList.SelectedItem is not string snap) return;

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = Path.Combine(_destination, snap),
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
