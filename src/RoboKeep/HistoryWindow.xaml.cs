using System.Windows;
using System.Windows.Controls;
using RoboKeep.Core.Models;
using RoboKeep.Core.Services;
using RoboKeep.Localization;

namespace RoboKeep;

/// <summary>Riga della griglia cronologia (testi già formattati).</summary>
public sealed record HistoryRow(string When, string Job, string Kind, string Outcome,
    string Counts, string Duration, string? LogPath);

public partial class HistoryWindow : Wpf.Ui.Controls.FluentWindow
{
    private const string AllJobs = "*";
    private readonly AppHost _host;

    public HistoryWindow(AppHost host, string? initialJob)
    {
        InitializeComponent();
        _host = host;

        JobFilter.Items.Add(new ComboBoxItem { Content = Loc.Instance["Hist_AllJobs"], Tag = AllJobs });
        foreach (var j in _host.Config.Jobs)
            JobFilter.Items.Add(new ComboBoxItem { Content = j.Name, Tag = j.Name });

        var initialIndex = 0;
        if (initialJob is not null)
            for (var i = 1; i < JobFilter.Items.Count; i++)
                if ((string)((ComboBoxItem)JobFilter.Items[i]).Tag == initialJob) { initialIndex = i; break; }
        JobFilter.SelectedIndex = initialIndex;
    }

    private void OnFilterChanged(object sender, SelectionChangedEventArgs e) => Reload();

    private void Reload()
    {
        var tag = (JobFilter.SelectedItem as ComboBoxItem)?.Tag as string ?? AllJobs;
        var entries = _host.History.List(tag == AllJobs ? null : tag);
        HistoryGrid.ItemsSource = entries.Select(ToRow).ToList();
    }

    private static HistoryRow ToRow(RunHistoryEntry e)
    {
        var kind = e.Kind switch
        {
            "verify" => Loc.Instance["Hist_KindVerify"],
            "skipped" => Loc.Instance["Hist_KindSkipped"],
            _ => Loc.Instance["Hist_KindBackup"],
        };
        var outcome = e.Kind == "skipped"
            ? Loc.Instance["Hist_OutcomeSkipped"]
            : e.Success ? "OK" : Loc.Instance["Run_Error"];
        var counts = e.Kind switch
        {
            "skipped" => Loc.Instance["Hist_CountsSkipped"],
            "verify" => string.Format(Loc.Instance["Hist_CountsVerify"], e.FilesCopied, e.FilesFailed, e.FilesSkipped),
            _ => string.Format(Loc.Instance["Hist_CountsBackup"], e.FilesCopied, e.FilesSkipped, e.FilesFailed + e.DirsFailed),
        };
        var duration = (e.FinishedAt - e.StartedAt).ToString(@"hh\:mm\:ss");
        return new HistoryRow($"{e.StartedAt:dd/MM/yyyy HH:mm}", e.JobName, kind, outcome, counts, duration, e.LogPath);
    }

    private void OnOpenLog(object sender, RoutedEventArgs e)
    {
        if (HistoryGrid.SelectedItem is not HistoryRow row) return;
        if (row.LogPath is null)
        {
            // Le verifiche integrità non hanno un log dedicato: l'esito è nel riepilogo
            // della console, non serve (e sarebbe fuorviante) parlare di pulizia automatica.
            MessageBox.Show(Loc.Instance["Hist_NoLog"], row.Job,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var text = LogArchiveReader.ReadLogText(row.LogPath);
        if (text is null)
        {
            MessageBox.Show(Loc.Instance["Hist_LogGone"], row.Job,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var win = new LogViewerWindow($"{row.Job} — {row.When}", text) { Owner = this };
        win.ShowDialog();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
