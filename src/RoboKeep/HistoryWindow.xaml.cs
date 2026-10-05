using System.Windows;
using System.Windows.Controls;
using RoboKeep.Core.Models;
using RoboKeep.Core.Services;
using RoboKeep.Localization;

namespace RoboKeep;

/// <summary>Riga della griglia cronologia (testi già formattati).</summary>
/// <param name="NoLogKey">Il testo da mostrare se la voce non ha un log: per un job saltato dice
/// il perché.</param>
public sealed record HistoryRow(string When, string Job, string Kind, string Outcome,
    string Counts, string Duration, string? LogPath, string NoLogKey = "Hist_NoLog");

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
            RunHistoryEntry.KindVerify => Loc.Instance["Hist_KindVerify"],
            RunHistoryEntry.KindSkipped => Loc.Instance["Hist_KindSkipped"],
            _ => Loc.Instance["Hist_KindBackup"],
        };
        var outcome = e.IsSkipped
            ? Loc.Instance["Hist_OutcomeSkipped"]
            : e.IsCancelled ? Loc.Instance["Run_Cancelled"]
            : e.Success ? "OK" : Loc.Instance["Run_Error"];
        var skip = SkipTextKeys(e.Reason);
        var counts = e.Kind switch
        {
            RunHistoryEntry.KindSkipped => Loc.Instance[skip.Counts],
            RunHistoryEntry.KindCancelled => Loc.Instance["Hist_CountsCancelled"],
            RunHistoryEntry.KindVerify => string.Format(Loc.Instance["Hist_CountsVerify"], e.FilesCopied, e.FilesFailed, e.FilesSkipped),
            _ => string.Format(Loc.Instance["Hist_CountsBackup"], e.FilesCopied, e.FilesSkipped, e.FilesFailed + e.DirsFailed),
        };
        var duration = (e.FinishedAt - e.StartedAt).ToString(@"hh\:mm\:ss");
        return new HistoryRow($"{e.StartedAt:dd/MM/yyyy HH:mm}", e.JobName, kind, outcome, counts, duration,
            e.LogPath, e.IsSkipped ? skip.NoLog : "Hist_NoLog");
    }

    /// <summary>Le chiavi dei testi di un job saltato, secondo il motivo: disco atteso non collegato
    /// (motivo null, il caso storico), stesso job gia' in esecuzione, destinazione occupata.</summary>
    public static (string Counts, string NoLog) SkipTextKeys(string? reason) => reason switch
    {
        RunHistoryEntry.ReasonAlreadyRunning => ("Hist_CountsAlreadyRunning", "Hist_NoLogAlreadyRunning"),
        RunHistoryEntry.ReasonDestinationBusy => ("Hist_CountsDestinationBusy", "Hist_NoLogDestinationBusy"),
        _ => ("Hist_CountsSkipped", "Hist_NoLogSkipped"),
    };

    private void OnOpenLog(object sender, RoutedEventArgs e)
    {
        if (HistoryGrid.SelectedItem is not HistoryRow row) return;
        if (row.LogPath is null)
        {
            // Due casi diversi, entrambi senza log ma per motivi opposti: la verifica ha girato
            // e il suo esito è nel riepilogo della console; il job saltato non ha girato affatto.
            // Chi apre un salto sta cercando proprio il perché: parlargli di verifiche o di
            // pulizia automatica lo manderebbe fuori strada.
            MessageBox.Show(Loc.Instance[row.NoLogKey], row.Job,
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

        // Il log si apre nel Blocco note: ricerca (Ctrl+F), copia e salvataggio, e non blocca la
        // cronologia come farebbe una finestra modale. I log sono in .zip, quindi se ne estrae una
        // copia usa-e-getta. Se il Blocco note non parte, resta il visualizzatore interno.
        try
        {
            var viewerDir = System.IO.Path.Combine(_host.Config.Settings.TempRoot, "viewer");
            if (LogArchiveReader.ExtractForViewing(row.LogPath, viewerDir) is { } copy)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = $"\"{copy}\"",
                    UseShellExecute = true,
                });
                return;
            }
        }
        catch { /* ripiego qui sotto */ }

        var win = new LogViewerWindow($"{row.Job} — {row.When}", text) { Owner = this };
        win.ShowDialog();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
