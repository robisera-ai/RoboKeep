using RobocopySW.Core.Models;
using RobocopySW.Infra;

namespace RobocopySW.ViewModels;

/// <summary>Riga della griglia: incapsula un <see cref="BackupJob"/> più stato a runtime.</summary>
public sealed class JobViewModel : ObservableObject
{
    public BackupJob Model { get; }

    public JobViewModel(BackupJob model) => Model = model;

    public string Name => Model.Name;
    public string Source => Model.Source;
    public string Destination => Model.Destination;
    public bool Mirror => Model.Mirror;
    public bool Enabled
    {
        get => Model.Enabled;
        set { Model.Enabled = value; OnPropertyChanged(); }
    }

    public string ModeLabel => Model.Mirror ? "Mirror" : "Solo copia";

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

    /// <summary>Notifica la UI dopo una modifica del modello sottostante.</summary>
    public void RefreshAll()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Source));
        OnPropertyChanged(nameof(Destination));
        OnPropertyChanged(nameof(Mirror));
        OnPropertyChanged(nameof(Enabled));
        OnPropertyChanged(nameof(ModeLabel));
    }
}
