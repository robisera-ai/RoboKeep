using System.Runtime.InteropServices;
using System.Windows;
using Wpf.Ui.Appearance;

namespace RoboKeep;

/// <summary>
/// Entry point dell'applicazione. In presenza di argomenti CLI (es. <c>--run-all</c>,
/// <c>--job "Nome"</c>) esegue in modalità silenziosa e termina con un exit code;
/// altrimenti avvia la GUI.
/// </summary>
public partial class App : Application
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);
    private const int AttachParentProcess = -1;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var options = CliOptions.Parse(e.Args);
        if (options.SmartHelperDir is not null)
        {
            // Helper elevato per lo SMART: legge e scrive smart.json, poi esce. Niente GUI.
            Shutdown(RoboKeep.Core.Services.Smart.SmartHelper.Run(options.SmartHelperDir));
            return;
        }

        if (options.VssHelperDir is not null)
        {
            // Modalità helper elevato: crea lo snapshot, attende il rilascio, pulisce, esce.
            // Niente GUI, niente localizzazione: il processo comunica solo via file di sessione.
            var helperExit = RoboKeep.Core.Services.VssHelper.Run(options.VssHelperDir);
            Shutdown(helperExit);
            return;
        }

        if (options.HasCommand)
        {
            // Modalità headless: aggancia la console del processo padre (se presente) per l'output.
            AttachConsole(AttachParentProcess);
            var host = AppHost.Load(options.ConfigPath);
            Localization.Loc.Instance.ApplyFromSetting(host.Config.Settings.Language);
            var exit = await host.RunHeadlessAsync(options);
            Shutdown(exit);
            return;
        }

        // Rete di sicurezza della GUI: un'eccezione non gestita (es. in un gestore async void)
        // chiudeva l'app senza una parola, con il lavoro in corso perso. Ora finisce in
        // crash.log nella cartella dati, l'utente vede cosa e' successo e l'app resta aperta.
        DispatcherUnhandledException += (_, args) =>
        {
            var path = RoboKeep.Core.Services.CrashLog.Write(args.Exception, "UI");
            args.Handled = true;
            try
            {
                MessageBox.Show(
                    string.Format(Localization.Loc.Instance["App_CrashBody"], args.Exception.Message, path ?? "-"),
                    Localization.Loc.Instance["App_CrashTitle"], MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { /* se non riusciamo nemmeno a mostrare il messaggio, il log basta */ }
        };
        // Thread non UI e task dimenticati: qui non si puo' impedire la chiusura, ma almeno resta traccia.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex) RoboKeep.Core.Services.CrashLog.Write(ex, "thread");
        };
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            RoboKeep.Core.Services.CrashLog.Write(args.Exception, "task");
            args.SetObserved();
        };

        // Applica il tema (chiaro/scuro) seguendo le impostazioni di sistema.
        ApplicationThemeManager.ApplySystemTheme();

        var window = new MainWindow();
        window.Show();
    }
}
