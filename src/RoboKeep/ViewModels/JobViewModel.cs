using RoboKeep.Core.Models;
using RoboKeep.Infra;
using RoboKeep.Localization;

namespace RoboKeep.ViewModels;

/// <summary>Riga della griglia: incapsula un <see cref="BackupJob"/> più stato a runtime.</summary>
public sealed class JobViewModel : ObservableObject
{
    public BackupJob Model { get; }

    public JobViewModel(BackupJob model)
    {
        Model = model;
        // Aggiorna l'etichetta modalità quando cambia la lingua.
        Loc.Instance.PropertyChanged += (_, _) => OnPropertyChanged(nameof(ModeLabel));
    }

    public string Name => Model.Name;
    public string Source => Model.Source;
    public string Destination => Model.Destination;
    public bool Enabled
    {
        get => Model.Enabled;
        set { Model.Enabled = value; OnPropertyChanged(); }
    }

    public string ModeLabel => Model.Mirror ? Loc.Instance["Mode_Mirror"] : Loc.Instance["Mode_CopyOnly"];

    private string _lastStatus = "—";
    public string LastStatus
    {
        get => _lastStatus;
        set => SetField(ref _lastStatus, value);
    }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        set => SetField(ref _isRunning, value);
    }

    private BackupHealth _health = BackupHealth.Ok;
    public BackupHealth Health
    {
        get => _health;
        set => SetField(ref _health, value);
    }

    // Tooltip dell'icona di avviso sulla riga; null = nessun problema (icona nascosta).
    private string? _healthTooltip;
    public string? HealthTooltip
    {
        get => _healthTooltip;
        set => SetField(ref _healthTooltip, value);
    }

    /// <summary>Imposta l'esito a partire dall'ultimo risultato persistito (o "—" se assente).</summary>
    public void ApplyLastResult(JobLastResult? r)
    {
        // Un job fermato dalla guardia sulle cancellazioni non ha conteggi da raccontare: non e'
        // partito. Al loro posto va il motivo, che e' l'unica cosa che serve sapere.
        LastStatus = r is null
            ? "—"
            : r.DeletionsBlocked
            ? $"{RoboKeep.Core.CoreLoc.S("Guard_Status")}  ({r.FinishedAt:dd/MM HH:mm})"
            : RunStatus.Format(r.Success, r.FilesCopied, r.FilesSkipped, r.FilesExtra, r.FilesFailed + r.DirsFailed, r.FinishedAt);
    }

    /// <summary>Notifica la UI dopo una modifica del modello sottostante.</summary>
    public void RefreshAll()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Source));
        OnPropertyChanged(nameof(Destination));
        OnPropertyChanged(nameof(Enabled));
        OnPropertyChanged(nameof(ModeLabel));
    }
}
