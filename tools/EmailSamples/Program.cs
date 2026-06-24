using System.Globalization;
using RobocopySW.Core.Models;
using RobocopySW.Core.Services;

// Invia email di esempio (produzione) in 5 lingue: per ciascuna un backup riuscito e uno fallito.
// Riusa il vero EmailService; legge SMTP/credenziali dal config.json dell'app.

var configPath = args.Length > 0
    ? args[0]
    : @"d:\Progetti\RobocopySW\src\RobocopySW\bin\Debug\net10.0-windows\config.json";

var config = new ConfigStore(configPath).Load();
var settings = config.Settings.Email;
settings.Enabled = true;     // forza l'invio
settings.OnlyOnError = false; // invia anche i successi

var creds = new CredentialService(config.Settings.CredentialScope);
var email = new EmailService(creds);

(string code, string name)[] langs =
{
    ("it", "Italiano"), ("en", "English"), ("es", "Español"), ("fr", "Français"), ("de", "Deutsch"),
};

foreach (var (code, name) in langs)
{
    var ci = new CultureInfo(code);
    CultureInfo.CurrentUICulture = ci;
    CultureInfo.DefaultThreadCurrentUICulture = ci;

    var ok = MakeResult($"Documenti ({name})", success: true);
    var fail = MakeResult($"Progetti ({name})", success: false);

    await Send(ok, "OK   ");
    await Send(fail, "ERROR");
}

Console.WriteLine("Fatto: 10 email inviate (controlla l'inbox Mailtrap).");

async Task Send(JobResult result, string label)
{
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            await email.SendResultAsync(settings, result);
            Console.WriteLine($"[{result.JobName}] {label} inviata");
            await Task.Delay(6000); // il piano Mailtrap free limita gli invii/sec
            return;
        }
        catch (Exception ex) when (attempt < 6 && (ex.Message.Contains("Too many") || ex.InnerException?.Message.Contains("Too many") == true))
        {
            Console.WriteLine($"[{result.JobName}] {label} limite raggiunto, attendo 15s (tentativo {attempt})…");
            await Task.Delay(15000);
        }
    }
}

static JobResult MakeResult(string job, bool success)
{
    var exit = success ? 1 : 8;
    var interp = ExitCodeInterpreter.Interpret(exit);
    return new JobResult
    {
        JobName = job,
        ExitCode = exit,
        Success = interp.Success,
        Status = interp.Summary,
        StartedAt = DateTime.Now,
        Duration = success ? TimeSpan.FromSeconds(133) : TimeSpan.FromSeconds(47),
        DryRun = false,
        DirsCopied = success ? 18 : 6,
        FilesCopied = success ? 342 : 120,
        FilesSkipped = success ? 5120 : 900,
        FilesExtra = success ? 7 : 0,
        FilesFailed = success ? 0 : 4,
    };
}
