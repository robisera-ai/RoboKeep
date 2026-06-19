namespace RobocopySW;

/// <summary>Opzioni da riga di comando per l'esecuzione silenziosa (schedulazione).</summary>
public sealed class CliOptions
{
    public bool RunAll { get; private set; }
    public string? JobName { get; private set; }
    public bool DryRun { get; private set; }

    /// <summary>Percorso alternativo del file di configurazione (facoltativo).</summary>
    public string? ConfigPath { get; private set; }

    /// <summary>true se è stata richiesta un'esecuzione headless (niente GUI).</summary>
    public bool HasCommand => RunAll || !string.IsNullOrEmpty(JobName);

    public static CliOptions Parse(string[] args)
    {
        var o = new CliOptions();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--run-all":
                case "/run-all":
                    o.RunAll = true;
                    break;
                case "--job":
                case "/job":
                    if (i + 1 < args.Length)
                        o.JobName = args[++i];
                    break;
                case "--dry-run":
                case "/dry-run":
                case "--preview":
                    o.DryRun = true;
                    break;
                case "--config":
                case "/config":
                    if (i + 1 < args.Length)
                        o.ConfigPath = args[++i];
                    break;
            }
        }
        return o;
    }
}
