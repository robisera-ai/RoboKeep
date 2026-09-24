using System.Collections.ObjectModel;
using RoboKeep.Core.Models;
using RoboKeep.Core.Services;
using RoboKeep.Infra;
using RoboKeep.Localization;

namespace RoboKeep.ViewModels;

/// <summary>Una riga della finestra: testi già tradotti e formattati, pronti da mostrare.</summary>
public sealed class ScheduledTaskRow
{
    /// <summary>Nome dell'attività in Windows: è quello che serve per eliminarla.</summary>
    public string Name { get; init; } = "";
    /// <summary>Job collegato, «Avvia tutti», oppure il motivo per cui è orfana.</summary>
    public string LinkText { get; init; } = "";
    /// <summary>true quando l'attività non ha più un padrone: la riga si colora d'avviso.</summary>
    public bool IsOrphan { get; init; }
    public string NextRunText { get; init; } = "";
    public string LastRunText { get; init; } = "";
    /// <summary>Comando registrato con i suoi argomenti: dice quale copia dell'app parte.</summary>
    public string CommandText { get; init; } = "";
    /// <summary>Nome del job (come in configurazione) che ha generato l'attività; null per
    /// «Avvia tutti» e per le orfane. Eliminando l'attività si toglie la pianificazione a lui.</summary>
    public string? JobName { get; init; }
}

/// <summary>
/// Stato della finestra «Attività pianificate»: l'elenco delle sole attività di RoboKeep,
/// con il legame a un job o il motivo dell'orfanità. Sola lettura più l'eliminazione: la
/// pianificazione si cambia nell'editor del job, unica sorgente dell'attività.
/// </summary>
public sealed class ScheduledTasksViewModel : ObservableObject
{
    private readonly AppHost _host;

    public ObservableCollection<ScheduledTaskRow> Rows { get; } = new();

    private bool _busy;
    public bool IsBusy
    {
        get => _busy;
        private set
        {
            if (SetField(ref _busy, value))
                OnPropertyChanged(nameof(CanAct));
        }
    }

    /// <summary>Negazione di <see cref="IsBusy"/>: spegne i pulsanti durante la lettura.</summary>
    public bool CanAct => !_busy;

    private bool _isEmpty;
    /// <summary>true quando non c'è nessuna attività: la finestra lo dice invece di restare vuota.</summary>
    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetField(ref _isEmpty, value);
    }

    private string _status = "";
    /// <summary>Riga di stato: vuota quando va tutto bene, altrimenti l'errore dell'ultima azione.</summary>
    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    /// <summary>true se un'eliminazione ha tolto la pianificazione a un job: la finestra
    /// principale deve ricaricare l'elenco dei job.</summary>
    public bool JobsChanged { get; private set; }

    public ScheduledTasksViewModel(AppHost host) => _host = host;

    /// <summary>Rilegge l'elenco dall'Utilità di pianificazione e ricostruisce le righe.</summary>
    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        // L'errore dell'azione precedente parlava dell'elenco di prima: tenerlo a video
        // accanto a un elenco appena riletto lo farebbe leggere come un guasto ancora aperto.
        Status = "";
        try
        {
            var jobNames = _host.Config.Jobs.Select(j => j.Name).ToList();
            var exe = Environment.ProcessPath ?? "";
            // La lettura COM può metterci qualche decimo di secondo: mai sul thread della UI.
            var rows = await Task.Run(() =>
                TaskSchedulerCatalog.List().Select(t => Build(t, jobNames, exe)).ToList());
            Rows.Clear();
            foreach (var row in rows)
                Rows.Add(row);
            IsEmpty = Rows.Count == 0;
        }
        catch (Exception ex)
        {
            Status = ex.Message;
            // Senza righe a video lo stato vuoto deve comparire lo stesso: la finestra muta
            // e senza elenco non direbbe nulla nemmeno del proprio fallimento.
            IsEmpty = Rows.Count == 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Elimina l'attività e ricarica l'elenco. Se l'attività era di un job, al job viene tolta
    /// la pianificazione: altrimenti l'editor direbbe «giornaliera» senza che nulla parta, e al
    /// primo salvataggio l'attività appena eliminata ricomparirebbe.
    /// </summary>
    public async Task DeleteAsync(ScheduledTaskRow row)
    {
        if (IsBusy) return;
        IsBusy = true;
        var ok = false;
        try
        {
            var name = row.Name;
            ok = await Task.Run(() => TaskSchedulerCatalog.Delete(name));
            // Sempre il nome dell'attività: è quello che l'utente ha davanti e che deve
            // ritrovare nell'elenco. Il motivo tecnico, quando c'è, si aggiunge in coda.
            Status = ok ? "" : string.Format(Loc.Instance["Tasks_DeleteFailed"], row.Name);
            if (ok && row.JobName is { } jobName
                && _host.Config.Jobs.FirstOrDefault(j => j.Name == jobName) is { } job
                && job.Schedule != ScheduleKind.None)
            {
                job.Schedule = ScheduleKind.None;
                _host.SaveConfig();
                JobsChanged = true;
            }
        }
        catch (Exception ex)
        {
            Status = string.Format(Loc.Instance["Tasks_DeleteFailedWithReason"], row.Name, ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
        // Ricarica sempre, anche quando l'eliminazione fallisce: l'attività potrebbe essere
        // sparita per conto suo e l'elenco a video sarebbe una bugia. La ricarica azzera la
        // riga di stato, quindi l'errore dell'eliminazione va rimesso dopo — a meno che la
        // ricarica stessa non sia fallita: in quel caso il guasto più recente conta di più.
        var failure = ok ? null : Status;
        await RefreshAsync();
        if (failure is not null && Status.Length == 0)
            Status = failure;
    }

    private static ScheduledTaskRow Build(ScheduledTaskInfo task, IReadOnlyList<string> jobNames, string exe)
    {
        var link = ScheduledTaskInfo.Classify(task, jobNames, exe);
        // Il nome del job viene dalla configurazione, non dal suffisso dell'attività: quello
        // è sanificato, e un job «Foto/2026» comparirebbe qui come «Foto_2026».
        var jobName = ScheduledTaskInfo.MatchingJobName(task, jobNames);
        var linkText = link switch
        {
            TaskLink.Job => string.Format(Loc.Instance["Tasks_LinkJob"],
                jobName ?? ScheduledTaskInfo.JobNameOf(task.Name) ?? task.Name),
            TaskLink.RunAll => Loc.Instance["Tasks_LinkRunAll"],
            TaskLink.OrphanOtherExe => Loc.Instance["Tasks_OrphanOtherExe"],
            _ => Loc.Instance["Tasks_OrphanNoJob"],
        };

        var next = task.NextRun is { } n
            ? string.Format(Loc.Instance["Tasks_NextRun"], n.ToString("g"))
            : Loc.Instance["Tasks_NextRunNone"];
        // Lo stato sta accanto alla prossima esecuzione perché è lì che cambia il senso della
        // riga: un'attività disattivata ha una data e non partirà lo stesso.
        var state = task.State switch
        {
            1 => Loc.Instance["Tasks_StateDisabled"],
            4 => Loc.Instance["Tasks_StateRunning"],
            _ => "",
        };
        if (state.Length > 0) next += " — " + state;

        // 267011 è il codice di «mai eseguita»: mostrarlo come esito sarebbe un errore inventato.
        var never = task.LastRun is null || task.LastResult == ScheduledTaskInfo.NeverRunResult;
        var lastRun = never
            ? Loc.Instance["Tasks_LastRunNever"]
            : string.Format(Loc.Instance["Tasks_LastRun"], task.LastRun!.Value.ToString("g"),
                task.LastResult == 0 ? Loc.Instance["Tasks_ResultOk"] : task.LastResult?.ToString() ?? "?");

        var command = string.IsNullOrEmpty(task.Arguments) ? task.Command : task.Command + " " + task.Arguments;

        return new ScheduledTaskRow
        {
            Name = task.Name,
            LinkText = linkText,
            IsOrphan = link is TaskLink.OrphanNoJob or TaskLink.OrphanOtherExe,
            NextRunText = next,
            LastRunText = lastRun,
            CommandText = command,
            JobName = link == TaskLink.Job ? jobName : null,
        };
    }
}
