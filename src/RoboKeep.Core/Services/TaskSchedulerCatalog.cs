using System.Runtime.InteropServices;

namespace RoboKeep.Core.Services;

/// <summary>
/// Legge ed elimina le attivita' di RoboKeep nella cartella radice dell'Utilita' di
/// pianificazione, tramite l'API COM <c>Schedule.Service</c> in late binding.
/// Non si usa l'output CSV di <c>schtasks /Query</c>: le virgolette dentro «Task To Run»
/// non sono protette e il comando di un job con spazi nel percorso arriverebbe spezzato.
/// La creazione resta a <see cref="SchedulerService"/>.
/// Tutto e' best-effort: se l'API non c'e' o l'accesso e' negato, <see cref="List"/>
/// restituisce un elenco vuoto e <see cref="Delete"/> false, senza mai sollevare eccezioni.
/// </summary>
public static class TaskSchedulerCatalog
{
    // GetTasks(1) = TASK_ENUM_HIDDEN: include anche le attivita' nascoste, che altrimenti
    // resterebbero invisibili proprio mentre partono di notte.
    private const int IncludeHidden = 1;

    // Type = 0 nell'enumerazione TASK_ACTION_TYPE: esecuzione di un programma.
    private const int ExecAction = 0;

    /// <summary>Attivita' di RoboKeep presenti nella cartella radice, nell'ordine di Windows.</summary>
    public static IReadOnlyList<ScheduledTaskInfo> List()
    {
        var found = new List<ScheduledTaskInfo>();
        object? service = null;
        try
        {
            service = CreateService();
            if (service is null) return found;
            dynamic svc = service;
            dynamic folder = svc.GetFolder("\\");
            dynamic tasks = folder.GetTasks(IncludeHidden);

            // Le collezioni COM partono da 1 e non si lasciano percorrere con foreach
            // quando l'oggetto e' dynamic: si usano Count e Item.
            int count = tasks.Count;
            for (var i = 1; i <= count; i++)
            {
                try
                {
                    dynamic task = tasks.Item(i);
                    string name = task.Name;
                    if (!ScheduledTaskInfo.IsRoboKeep(name)) continue;
                    found.Add(Read(task, name));
                }
                catch
                {
                    // Una singola attivita' illeggibile non deve svuotare tutto l'elenco.
                }
            }
        }
        catch
        {
            return Array.Empty<ScheduledTaskInfo>();
        }
        finally
        {
            Release(service);
        }
        return found;
    }

    /// <summary>
    /// Elimina l'attivita' dalla cartella radice. Solo attivita' di RoboKeep: un nome altrui
    /// viene rifiutato senza nemmeno provarci.
    /// </summary>
    public static bool Delete(string taskName)
    {
        if (!ScheduledTaskInfo.IsRoboKeep(taskName)) return false;
        // I nomi delle attivita' sono segmenti di percorso: con una barra si esce dalla
        // cartella radice e il prefisso RoboKeep non protegge piu' niente
        // ("RoboKeep_x\..\Microsoft\Windows\..."). Qui si cancella, quindi si rifiuta e basta.
        if (taskName.Contains('\\') || taskName.Contains('/')) return false;
        object? service = null;
        try
        {
            service = CreateService();
            if (service is null) return false;
            dynamic svc = service;
            dynamic folder = svc.GetFolder("\\");
            folder.DeleteTask(taskName, 0);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            Release(service);
        }
    }

    /// <summary>Servizio COM gia' connesso all'account corrente, null se non disponibile.</summary>
    private static object? CreateService()
    {
        var type = Type.GetTypeFromProgID("Schedule.Service");
        if (type is null) return null;
        var instance = Activator.CreateInstance(type);
        if (instance is null) return null;
        try
        {
            dynamic svc = instance;
            svc.Connect();
            return instance;
        }
        catch
        {
            // Connect fallito (servizio fermo, accesso negato): l'oggetto COM e' gia' nato e
            // nessuno lo rilascerebbe, perche' il chiamante riceve null.
            Release(instance);
            return null;
        }
    }

    private static void Release(object? comObject)
    {
        if (comObject is null) return;
        try { Marshal.FinalReleaseComObject(comObject); } catch { }
    }

    /// <summary>Copia in memoria i campi che servono: dopo, l'oggetto COM si puo' lasciare andare.</summary>
    private static ScheduledTaskInfo Read(dynamic task, string name)
    {
        DateTime? next = null;
        DateTime? last = null;
        int? result = null;
        var state = 0;
        var command = "";
        var arguments = "";

        try { DateTime raw = task.NextRunTime; next = Moment(raw); } catch { }
        try { DateTime raw = task.LastRunTime; last = Moment(raw); } catch { }
        try { int raw = task.LastTaskResult; result = raw; } catch { }
        try { int raw = task.State; state = raw; } catch { }

        try
        {
            dynamic actions = task.Definition.Actions;
            int count = actions.Count;
            for (var i = 1; i <= count; i++)
            {
                dynamic action = actions.Item(i);
                int type = action.Type;
                if (type != ExecAction) continue;
                command = (string?)action.Path ?? "";
                arguments = (string?)action.Arguments ?? "";
                break; // RoboKeep ne registra sempre una sola: la prima e' quella buona
            }
        }
        catch { }

        return new ScheduledTaskInfo(name, next, last, result, state, command.Trim(), arguments.Trim());
    }

    /// <summary>
    /// Data reale oppure null. Quando la voce non esiste l'Utilita' di pianificazione non
    /// lascia il campo vuoto: restituisce una data sentinella (30/11/1999 per «mai eseguita»,
    /// 1601 o 1899 in altri casi). Qualunque anno prima del 2000 vale «nessuna data».
    /// </summary>
    private static DateTime? Moment(DateTime value) => value.Year < 2000 ? null : value;
}
