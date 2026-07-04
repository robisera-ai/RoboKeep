namespace RoboKeep.Core.Models;

/// <summary>Risposte della creazione guidata di un job: input puro per <see cref="Services.JobWizardPlanner"/>.</summary>
public sealed class JobWizardAnswers
{
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public string Destination { get; set; } = "";
    public StorageKind SourceStorage { get; set; } = StorageKind.Ssd;
    public StorageKind DestStorage { get; set; } = StorageKind.Ssd;
    public bool Mirror { get; set; } = true;
    public bool HasLargeFiles { get; set; }
    public List<string> FrozenMetadataPatterns { get; set; } = new();
    public bool PreservePermissions { get; set; }
    public bool ExcludeCommonTemp { get; set; }
    public bool HasOpenFiles { get; set; }
}
