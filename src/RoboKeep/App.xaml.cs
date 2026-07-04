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

        // Applica il tema (chiaro/scuro) seguendo le impostazioni di sistema.
        ApplicationThemeManager.ApplySystemTheme();

        var window = new MainWindow();
        window.Show();
    }
}
