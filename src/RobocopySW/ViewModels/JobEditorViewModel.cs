using RobocopySW.Core.Models;
using RobocopySW.Core.Services;
using RobocopySW.Infra;

namespace RobocopySW.ViewModels;

/// <summary>Opzione di credenziale per la combo (id + etichetta leggibile).</summary>
public sealed record CredentialOption(string? Id, string Display);

/// <summary>ViewModel per la finestra di modifica di un job, con anteprima live del comando.</summary>
public sealed class JobEditorViewModel : ObservableObject
{
    private readonly BackupJob _job;

    public JobEditorViewModel(BackupJob job, IEnumerable<CredentialEntry> credentials)
    {
        _job = job;
        Credentials = new List<CredentialOption> { new(null, "(nessuna — percorso locale)") };
        foreach (var c in credentials)
            Credentials.Add(new CredentialOption(c.Id, $"{c.Id} ({c.Host})"));
        _selectedCredential = Credentials.FirstOrDefault(o => o.Id == job.CredentialId) ?? Credentials[0];
    }

    public BackupJob Job => _job;
    public List<CredentialOption> Credentials { get; }

    public string Name
    {
        get => _job.Name;
        set { _job.Name = value; OnPropertyChanged(); }
    }

    public string Source
    {
        get => _job.Source;
        set { _job.Source = value; OnPropertyChanged(); RaisePreview(); }
    }

    public string Destination
    {
        get => _job.Destination;
        set { _job.Destination = value; OnPropertyChanged(); RaisePreview(); }
    }

    public bool Mirror
    {
        get => _job.Mirror;
        set { _job.Mirror = value; OnPropertyChanged(); RaisePreview(); }
    }

    public bool ExcludeOlder
    {
        get => _job.ExcludeOlder;
        set { _job.ExcludeOlder = value; OnPropertyChanged(); RaisePreview(); }
    }

    public bool CopyAll
    {
        get => _job.CopyAll;
        set { _job.CopyAll = value; OnPropertyChanged(); RaisePreview(); }
    }

    public int MultiThread
    {
        get => _job.MultiThread;
        set { _job.MultiThread = value; OnPropertyChanged(); RaisePreview(); }
    }

    public int Retries
    {
        get => _job.Retries;
        set { _job.Retries = value; OnPropertyChanged(); RaisePreview(); }
    }

    public int Wait
    {
        get => _job.Wait;
        set { _job.Wait = value; OnPropertyChanged(); RaisePreview(); }
    }

    public bool Enabled
    {
        get => _job.Enabled;
        set { _job.Enabled = value; OnPropertyChanged(); }
    }

    /// <summary>Esclusioni file, una per riga (es. <c>*.tmp</c>).</summary>
    public string ExcludeFilesText
    {
        get => string.Join(Environment.NewLine, _job.ExcludeFiles);
        set { _job.ExcludeFiles = SplitLines(value); OnPropertyChanged(); RaisePreview(); }
    }

    /// <summary>Esclusioni cartelle, una per riga (es. <c>cache</c>).</summary>
    public string ExcludeDirsText
    {
        get => string.Join(Environment.NewLine, _job.ExcludeDirs);
        set { _job.ExcludeDirs = SplitLines(value); OnPropertyChanged(); RaisePreview(); }
    }

    private CredentialOption _selectedCredential;
    public CredentialOption SelectedCredential
    {
        get => _selectedCredential;
        set { _selectedCredential = value; _job.CredentialId = value?.Id; OnPropertyChanged(); }
    }

    /// <summary>Anteprima live della riga di comando robocopy.</summary>
    public string CommandPreview
    {
        get
        {
            try
            {
                return RobocopyArgsBuilder.ToDisplayString(RobocopyArgsBuilder.Build(_job));
            }
            catch (Exception ex)
            {
                return "(" + ex.Message + ")";
            }
        }
    }

    /// <summary>Validazione minima prima del salvataggio.</summary>
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) return "Il nome del job è obbligatorio.";
        if (string.IsNullOrWhiteSpace(Source)) return "La cartella sorgente è obbligatoria.";
        if (string.IsNullOrWhiteSpace(Destination)) return "La cartella destinazione è obbligatoria.";
        return null;
    }

    private void RaisePreview() => OnPropertyChanged(nameof(CommandPreview));

    private static List<string> SplitLines(string text) =>
        (text ?? "")
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
}
