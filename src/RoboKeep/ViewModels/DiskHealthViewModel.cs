using System.Collections.ObjectModel;
using RoboKeep.Core.Services;
using RoboKeep.Core.Services.Smart;
using RoboKeep.Infra;
using RoboKeep.Localization;

namespace RoboKeep.ViewModels;

/// <summary>Una riga del referto, già tradotta.</summary>
public sealed record DiskFindingRow(string Label, string Hint, string Value, DiskHealthLevel Level);

/// <summary>Una scheda disco: intestazione, verdetto, righe, registro eventi.</summary>
public sealed class DiskCardViewModel
{
    /// <summary>Intestazione: modello, bus e lettere, per esempio "SAMSUNG HD103SI — USB — E:".</summary>
    public string Title { get; init; } = "";
    public DiskHealthLevel Level { get; init; }
    public string LevelText { get; init; } = "";
    /// <summary>Spiegazione di un disco non leggibile (Smart_NeedsAdmin / Smart_BridgeNoSmart /
    /// Smart_ReadFailed).</summary>
    public string? Note { get; init; }
    /// <summary>true quando c'è una nota da mostrare: la riga resta nascosta negli altri casi.</summary>
    public bool HasNote => !string.IsNullOrEmpty(Note);
    public string EventsText { get; init; } = "";
    public IReadOnlyList<DiskFindingRow> Rows { get; init; } = Array.Empty<DiskFindingRow>();
}

/// <summary>
/// Stato della finestra «Salute dei dischi»: una lettura SMART per volta, le schede pronte da
/// mostrare e una riga di stato. Nessuna scrittura sui dischi: solo lettura.
/// </summary>
public sealed class DiskHealthViewModel : ObservableObject
{
    private readonly AppHost _host;

    public ObservableCollection<DiskCardViewModel> Disks { get; } = new();

    private bool _busy;
    public bool IsBusy
    {
        get => _busy;
        private set
        {
            if (SetField(ref _busy, value))
                OnPropertyChanged(nameof(CanRefresh));
        }
    }

    /// <summary>Negazione di <see cref="IsBusy"/>: lega l'abilitazione del pulsante Aggiorna
    /// senza bisogno di un convertitore.</summary>
    public bool CanRefresh => !_busy;

    private string _status = "";
    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    public DiskHealthViewModel(AppHost host) => _host = host;

    /// <summary>Rilegge lo SMART di tutti i dischi e ricostruisce le schede.</summary>
    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Status = Loc.Instance["Smart_Reading"];
        try
        {
            var snap = await SmartSession.ReadAsync(_host.SmartSessionRoot);
            // Il registro eventi di Windows costa circa un secondo per lettera: le schede si
            // costruiscono su un thread di lavoro, altrimenti la finestra resterebbe congelata.
            var cards = await Task.Run(() => snap.Disks.Select(d => Build(d, snap.ElevationDenied, snap.HelperFailed)).ToList());
            // Qui siamo di nuovo sul thread della UI (nessun ConfigureAwait): la collezione
            // osservabile si può aggiornare.
            Disks.Clear();
            foreach (var card in cards)
                Disks.Add(card);
            // Senza schede la finestra sarebbe vuota e muta: la riga di stato dice perché.
            Status = Disks.Count == 0 ? Loc.Instance["Smart_NoDisks"] : "";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Il più recente tra due istanti, ciascuno dei quali può mancare.</summary>
    private static DateTime? Later(DateTime? a, DateTime? b) =>
        a is null ? b : b is null ? a : a > b ? a : b;

    private static DiskCardViewModel Build(DiskReport d, bool elevationDenied, bool helperFailed)
    {
        var bus = Loc.Instance[d.Bus switch
        {
            DiskBus.Nvme => "Smart_Bus_Nvme",
            DiskBus.Sata => "Smart_Bus_Sata",
            DiskBus.Usb => "Smart_Bus_Usb",
            _ => "Smart_Bus_Other",
        }];
        var title = $"{d.Model} — {bus}" + (d.Letters.Length > 0 ? " — " + string.Join(", ", d.Letters) : "");
        var events = d.Letters.Select(l => DiskEventLog.Collect(l + @"\")).Aggregate(DiskEventSummary.None,
            (a, b) => new DiskEventSummary(a.BadBlocks + b.BadBlocks, a.IoErrors + b.IoErrors,
                a.FileSystemErrors + b.FileSystemErrors, Later(a.Latest, b.Latest)));
        var eventsText = string.Format(Loc.Instance["Smart_Events"], events.Total == 0 ? "0" : events.Describe());

        DiskVerdictResult? v = d.Nvme is { } n ? DiskVerdict.Evaluate(n) : d.Ata is { } a ? DiskVerdict.Evaluate(a) : null;
        if (v is null)
        {
            // Perché non si legge, in ordine di certezza: UAC negato (serve l'autorizzazione);
            // l'helper ha girato e il box USB non ha risposto al comando ATA (è il box, non il
            // disco); tutto il resto — helper fallito, disco scomparso tra le due letture,
            // controller muto — è una lettura non riuscita da ritentare.
            return new DiskCardViewModel
            {
                Title = title,
                Level = DiskHealthLevel.Unreadable,
                LevelText = Loc.Instance["Smart_Unreadable"],
                Note = Loc.Instance[
                    elevationDenied ? "Smart_NeedsAdmin"
                    : !helperFailed && d.Bus == DiskBus.Usb && d.Error == "ata" ? "Smart_BridgeNoSmart"
                    : "Smart_ReadFailed"],
                EventsText = eventsText,
            };
        }

        return new DiskCardViewModel
        {
            Title = title,
            Level = v.Level,
            LevelText = Loc.Instance[v.Level switch
            {
                DiskHealthLevel.Danger => "Smart_Danger",
                DiskHealthLevel.Warning => "Smart_Warning",
                _ => "Smart_Good",
            }],
            EventsText = eventsText,
            Rows = v.Findings
                .Select(f => new DiskFindingRow(Loc.Instance[f.Key], Loc.Instance[f.Key + "_Hint"], f.Value, f.Level))
                .ToList(),
        };
    }
}
