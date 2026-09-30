using System.Collections.ObjectModel;
using System.IO;
using RoboKeep.Core.Models;
using RoboKeep.Core.Services;
using RoboKeep.Infra;
using RoboKeep.Localization;

namespace RoboKeep.ViewModels;

/// <summary>Una voce dell'elenco «com'era il…»: «Adesso» (data null) oppure una versione.</summary>
public sealed class RestorePointItem
{
    public string Label { get; init; } = "";
    /// <summary>null = lo stato corrente del backup.</summary>
    public DateTime? Date { get; init; }
    /// <summary>Nome della cartella-versione (serve a preselezionare quella scelta in
    /// «Versioni...»); null per «Adesso».</summary>
    public string? Name { get; init; }
    public override string ToString() => Label;
}

/// <summary>
/// L'albero da spuntare, costruito dal piano: le cartelle si aprono a richiesta e le caselle
/// vivono in un'unica mappa di decisioni, non nei nodi.
/// <para>Perché così: il nodo di una cartella mai aperta non esiste, e un utente che spunta
/// «Foto» si aspetta comunque di portarsi via tutto quello che c'è sotto. Tenendo le decisioni
/// fuori dai nodi, una casella vale anche per i rami non ancora materializzati, sopravvive alla
/// ricerca (che rifà l'elenco a video) e permette l'eccezione: «tutta Foto, tranne questo file».</para>
/// </summary>
public sealed class RestoreTree
{
    private static readonly StringComparer Cmp = StringComparer.OrdinalIgnoreCase;
    private const StringComparison Ord = StringComparison.OrdinalIgnoreCase;

    /// <summary>Una voce dell'albero: figlia della cartella che la contiene.</summary>
    public sealed record Item(string Name, string RelativePath, bool IsFolder, long Size);

    // Percorso della cartella («» = radice) → le sue voci dirette, cartelle prima.
    private readonly Dictionary<string, List<Item>> _children = new(Cmp);

    // Decisioni esplicite dell'utente. Chi non c'è eredita dalla cartella che lo contiene.
    private readonly Dictionary<string, bool> _state = new(Cmp);

    /// <summary>Scatta quando una casella cambia: la finestra riaccende o spegne «Ripristina
    /// selezione».</summary>
    public event Action? SelectionChanged;

    public RestoreTree(RestorePlan plan)
    {
        var seen = new Dictionary<string, HashSet<string>>(Cmp);

        void EnsureFolder(string rel)
        {
            while (rel.Length > 0)
            {
                var parent = Path.GetDirectoryName(rel) ?? "";
                if (!Add(parent, new Item(Path.GetFileName(rel), rel, IsFolder: true, 0))) return;
                rel = parent;
            }
        }

        bool Add(string parent, Item item)
        {
            if (!seen.TryGetValue(parent, out var names)) seen[parent] = names = new HashSet<string>(Cmp);
            if (!names.Add(item.RelativePath)) return false;
            if (!_children.TryGetValue(parent, out var list)) _children[parent] = list = new List<Item>();
            list.Add(item);
            return true;
        }

        foreach (var rel in plan.Directories) EnsureFolder(rel);
        foreach (var (rel, entry) in plan.Files)
        {
            var parent = Path.GetDirectoryName(rel) ?? "";
            Add(parent, new Item(Path.GetFileName(rel), rel, IsFolder: false, entry.Size));
            EnsureFolder(parent);
        }

        foreach (var list in _children.Values)
            list.Sort((a, b) => a.IsFolder != b.IsFolder
                ? (a.IsFolder ? -1 : 1)
                : Cmp.Compare(a.Name, b.Name));
    }

    /// <summary>Le voci dirette di una cartella («» = radice).</summary>
    public IReadOnlyList<Item> ChildrenOf(string rel) =>
        _children.TryGetValue(rel, out var list) ? list : Array.Empty<Item>();

    /// <summary>true se almeno una casella è spuntata.</summary>
    public bool AnySelected => _state.ContainsValue(true);

    /// <summary>Stato effettivo di un percorso: la sua decisione, altrimenti quella della cartella
    /// più vicina che ne ha una, altrimenti «non spuntato».</summary>
    public bool Effective(string rel)
    {
        var p = rel;
        while (true)
        {
            if (_state.TryGetValue(p, out var v)) return v;
            var parent = Path.GetDirectoryName(p);
            if (string.IsNullOrEmpty(parent)) return false;
            p = parent;
        }
    }

    /// <summary>La casella da disegnare. Per una cartella conta il contenuto, non le decisioni:
    /// tutto spuntato = spuntata, niente = vuota, un po' e un po' = «a metà». Così una cartella i
    /// cui file sono stati spuntati uno per uno si mostra piena, non a metà.</summary>
    public bool? StateOf(string rel, bool isFolder)
    {
        var eff = Effective(rel);
        if (!isFolder) return eff;
        // Nessuna decisione sotto questa cartella: tutto il contenuto eredita la sua, niente da
        // contare (e' il caso comune, e sui rami grandi evita di scorrere migliaia di voci).
        if (!Under(rel, !eff) && !Under(rel, eff)) return eff;

        bool anyOn = false, anyOff = false;
        Walk(rel);
        return anyOn && anyOff ? null : anyOn;

        void Walk(string folder)
        {
            var children = ChildrenOf(folder);
            if (children.Count == 0)
            {
                // Una cartella vuota vale come una voce: si ripristina (vuota) o no.
                if (Effective(folder)) anyOn = true; else anyOff = true;
                return;
            }
            foreach (var child in children)
            {
                if (anyOn && anyOff) return;
                if (child.IsFolder) Walk(child.RelativePath);
                else if (Effective(child.RelativePath)) anyOn = true;
                else anyOff = true;
            }
        }
    }

    /// <summary>Registra una decisione dell'utente: quella della cartella vince su tutto ciò che
    /// c'è sotto, quindi le eccezioni precedenti si cancellano.</summary>
    public void Set(string rel, bool value)
    {
        foreach (var key in _state.Keys.Where(k => k.StartsWith(rel + '\\', Ord)).ToList())
            _state.Remove(key);
        _state[rel] = value;
        SelectionChanged?.Invoke();
    }

    /// <summary>«Seleziona tutto» / «Deseleziona tutto»: una decisione per ogni voce della radice,
    /// le eccezioni precedenti spariscono.</summary>
    public void SetAll(bool value)
    {
        _state.Clear();
        if (value)
            foreach (var item in ChildrenOf("")) _state[item.RelativePath] = true;
        SelectionChanged?.Invoke();
    }

    /// <summary>Azzera ogni decisione (cambio di data: l'albero non è più lo stesso).</summary>
    public void Clear()
    {
        _state.Clear();
        SelectionChanged?.Invoke();
    }

    /// <summary>
    /// I percorsi da passare a <see cref="RestorePlanner.Select"/>: il minor numero di voci che
    /// descrive esattamente ciò che è spuntato. Una cartella tutta spuntata vale una voce sola;
    /// se dentro c'è un'eccezione si scende di un livello, e solo lungo quel ramo.
    /// </summary>
    public IReadOnlyList<string> Selection()
    {
        var result = new List<string>();
        foreach (var item in ChildrenOf("")) Collect(item, result);
        return result;
    }

    private void Collect(Item item, List<string> result)
    {
        var rel = item.RelativePath;
        if (Effective(rel))
        {
            if (!Under(rel, false)) { result.Add(rel); return; }
        }
        else if (!Under(rel, true))
        {
            return;
        }

        foreach (var child in ChildrenOf(rel)) Collect(child, result);
    }

    /// <summary>true se sotto <paramref name="rel"/> c'è una decisione esplicita del valore dato:
    /// è ciò che rende una cartella «in parte spuntata».</summary>
    private bool Under(string rel, bool value) =>
        _state.Any(kv => kv.Value == value && kv.Key.StartsWith(rel + '\\', Ord));
}

/// <summary>Un nodo a video. Le cartelle si materializzano alla prima apertura: un backup di
/// centomila file non deve diventare centomila oggetti per mostrarne dieci.</summary>
public sealed class RestoreNode : ObservableObject
{
    private readonly RestoreTree _tree;
    private readonly RestoreNode? _parent;
    private bool _filled;

    public RestoreNode(RestoreTree tree, RestoreNode? parent, RestoreTree.Item item,
        string? label = null, bool placeholder = false)
    {
        _tree = tree;
        _parent = parent;
        IsPlaceholder = placeholder;
        Name = label ?? item.Name;
        RelativePath = item.RelativePath;
        IsFolder = item.IsFolder;
        SizeText = item.IsFolder || placeholder ? "" : VersionsUsage.Describe(item.Size);
        // Segnaposto: senza un figlio il TreeView non disegnerebbe la freccia di apertura, e la
        // cartella sembrerebbe vuota. Non ha una casella — spuntare «…» non vuol dire niente — e
        // sparisce alla prima apertura, sostituito dai figli veri.
        if (IsFolder && tree.ChildrenOf(RelativePath).Count > 0)
            Children.Add(new RestoreNode(tree, this,
                new RestoreTree.Item("…", RelativePath, IsFolder: false, 0), placeholder: true));
    }

    public string Name { get; }
    public string RelativePath { get; }
    public bool IsFolder { get; }
    public string SizeText { get; }

    /// <summary>true per il nodo finto che tiene aperta la freccia di una cartella non ancora
    /// materializzata: niente casella, niente icona, niente selezione.</summary>
    public bool IsPlaceholder { get; }

    public ObservableCollection<RestoreNode> Children { get; } = new();

    private bool _expanded;
    public bool IsExpanded
    {
        get => _expanded;
        set
        {
            if (!SetField(ref _expanded, value) || !value) return;
            Fill();
        }
    }

    public bool? IsChecked
    {
        get => _tree.StateOf(RelativePath, IsFolder);
        set
        {
            // La casella gira su tre stati per poter DISEGNARE una cartella spuntata a metà, ma
            // «a metà» non è una decisione che l'utente possa prendere: il clic che la
            // proporrebbe vale «niente». Il giro visto da chi clicca resta quindi spuntato /
            // non spuntato, e la cartella mostra comunque le eccezioni fatte al suo interno.
            _tree.Set(RelativePath, value ?? false);
            Refresh();
            _parent?.RefreshUp();
        }
    }

    private void Fill()
    {
        if (_filled) return;
        _filled = true;
        Children.Clear();
        foreach (var item in _tree.ChildrenOf(RelativePath))
            Children.Add(new RestoreNode(_tree, this, item));
    }

    /// <summary>Ridisegna la casella di questo nodo e di quelli già aperti sotto di lui.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(IsChecked));
        foreach (var child in Children) child.Refresh();
    }

    /// <summary>Ridisegna la casella dei padri: una cartella diventa «in parte spuntata» per
    /// colpa di un figlio.</summary>
    private void RefreshUp()
    {
        OnPropertyChanged(nameof(IsChecked));
        _parent?.RefreshUp();
    }
}

/// <summary>
/// Stato della finestra «Ripristina»: l'elenco delle date, l'albero di quel momento, la cartella
/// dove mettere i file e l'esecuzione della copia con avanzamento e annullamento.
/// <para>Non si scrive mai nella destinazione del backup né nella sorgente del job: un ripristino
/// legge e basta, e i file recuperati vanno dove decide l'utente.</para>
/// </summary>
public sealed class RestoreViewModel : ObservableObject
{
    private readonly BackupJob _job;
    private RestorePlan _plan = RestorePlan.Empty;
    private RestoreTree? _tree;
    private CancellationTokenSource? _cts;
    private string _warnedTarget = "";

    public RestoreViewModel(BackupJob job)
    {
        _job = job;
        Header = string.Format(Loc.Instance["Restore_Header"], job.Name);
    }

    public string Header { get; }

    public ObservableCollection<RestorePointItem> Points { get; } = new();
    public ObservableCollection<RestoreNode> Nodes { get; } = new();

    private RestorePointItem? _selectedPoint;
    public RestorePointItem? SelectedPoint
    {
        get => _selectedPoint;
        set
        {
            if (!SetField(ref _selectedPoint, value)) return;
            OnPropertyChanged(nameof(CanShowOnlyChanged));
            if (value is not null) _ = LoadPlanAsync();
        }
    }

    private bool _onlyChanged;
    /// <summary>Mostra solo i file che il backup scelto ha sostituito o cancellato (la sua
    /// cartella-versione), invece dell'albero completo di quel momento.</summary>
    public bool OnlyChanged
    {
        get => _onlyChanged;
        set
        {
            if (!SetField(ref _onlyChanged, value)) return;
            if (_selectedPoint?.Name is not null) _ = LoadPlanAsync();
        }
    }

    /// <summary>La casella ha senso solo per un backup passato: «Adesso» non ha una versione.</summary>
    public bool CanShowOnlyChanged => _selectedPoint?.Name is not null;

    private string _search = "";
    public string SearchText
    {
        get => _search;
        set { if (SetField(ref _search, value)) ShowNodes(); }
    }

    private string _target = "";
    public string TargetFolder
    {
        get => _target;
        set
        {
            if (!SetField(ref _target, value)) return;
            OnPropertyChanged(nameof(CanRestoreAll));
            OnPropertyChanged(nameof(CanRestoreSelection));
            CheckTarget();
        }
    }

    private string _status = "";
    /// <summary>Riga di stato: avvisi sulla cartella scelta, esito, errori.</summary>
    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    private bool _statusIsWarning;
    /// <summary>true quando la riga di stato è un avviso e va colorata. Un ripristino riuscito
    /// scritto in giallo si legge come un guasto: il colore è un messaggio, e deve dire il vero.</summary>
    public bool StatusIsWarning
    {
        get => _statusIsWarning;
        private set => SetField(ref _statusIsWarning, value);
    }

    private void SetStatus(string text, bool warning = false)
    {
        Status = text;
        StatusIsWarning = warning && text.Length > 0;
    }

    private string _totals = "";
    /// <summary>«n file · dimensione» del momento scelto.</summary>
    public string Totals
    {
        get => _totals;
        private set => SetField(ref _totals, value);
    }

    private bool _busy;
    public bool IsBusy
    {
        get => _busy;
        private set
        {
            if (!SetField(ref _busy, value)) return;
            OnPropertyChanged(nameof(CanAct));
            OnPropertyChanged(nameof(CanRestoreAll));
            OnPropertyChanged(nameof(CanRestoreSelection));
        }
    }

    /// <summary>Negazione di <see cref="IsBusy"/>: spegne i comandi durante una lettura o una copia.</summary>
    public bool CanAct => !_busy;

    private bool _copying;
    /// <summary>true mentre si copia: compaiono la barra e il pulsante Annulla.</summary>
    public bool IsCopying
    {
        get => _copying;
        private set => SetField(ref _copying, value);
    }

    private int _progress;
    public int Progress
    {
        get => _progress;
        private set => SetField(ref _progress, value);
    }

    private int _progressMax = 1;
    public int ProgressMax
    {
        get => _progressMax;
        private set => SetField(ref _progressMax, value);
    }

    private bool _isEmpty;
    /// <summary>true quando a quella data non c'era niente: la finestra lo dice invece di
    /// mostrare un riquadro vuoto.</summary>
    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetField(ref _isEmpty, value);
    }

    private string _failures = "";
    /// <summary>Elenco dei file non ripristinati, con il motivo.</summary>
    public string Failures
    {
        get => _failures;
        private set
        {
            if (SetField(ref _failures, value)) OnPropertyChanged(nameof(HasFailures));
        }
    }

    public bool HasFailures => _failures.Length > 0;

    private bool _done;
    /// <summary>true a ripristino finito: compare «Apri cartella».</summary>
    public bool IsDone
    {
        get => _done;
        private set => SetField(ref _done, value);
    }

    /// <summary>La cartella scelta va bene: c'è, e non è la sorgente del job né una sua
    /// sottocartella.</summary>
    public bool TargetValid =>
        _target.Trim().Length > 0
        && !RestoreCopier.IsSourceOrInside(_target, _job.Source)
        && !RestoreCopier.TouchesBackup(_target, _job.Destination);

    public bool CanRestoreAll => !_busy && TargetValid && _plan.Files.Count > 0;
    public bool CanRestoreSelection => CanRestoreAll && _tree is { AnySelected: true };

    /// <summary>La cartella dove sono finiti i file (per «Apri cartella»).</summary>
    public string RestoredTo { get; private set; } = "";

    /// <summary>L'ultimo ripristino è finito senza annullamento né file rimasti indietro: la
    /// finestra può chiudersi. Altrimenti resta aperta, perché l'elenco dei problemi è lì.</summary>
    public bool LastRestoreClean { get; private set; }

    /// <summary>Legge l'elenco delle date e sceglie quella indicata (o «Adesso»).</summary>
    public async Task LoadPointsAsync(string? preselect)
    {
        IsBusy = true;
        SetStatus(Loc.Instance["Restore_Loading"]);
        RestorePointItem? choice = null;
        try
        {
            var dest = (_job.Destination ?? "").Trim();
            // Leggere una destinazione con molte versioni (e i loro manifest) costa: mai sul
            // thread della UI, o la finestra si aprirebbe congelata.
            var items = await Task.Run(() => BuildPoints(dest));

            Points.Clear();
            Points.Add(new RestorePointItem { Label = Loc.Instance["Restore_Now"], Date = null });
            foreach (var item in items) Points.Add(item);

            SetStatus("");
            // Chi arriva da «Versioni...» ha scelto una cartella: il punto con lo stesso nome è in
            // elenco come «Prima del backup del…».
            choice = Points.FirstOrDefault(p => preselect is not null && p.Name == preselect)
                ?? Points[0];
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, warning: true);
        }
        finally
        {
            IsBusy = false;
        }

        // La data si sceglie qui, non dentro il try: passando dal setter la finestra si
        // riaccenderebbe a metà, mentre l'albero di quella data è ancora da leggere.
        if (choice is null) return;
        _selectedPoint = choice;
        OnPropertyChanged(nameof(SelectedPoint));
        await LoadPlanAsync();
    }

    /// <summary>
    /// Le date selezionabili, dalla più recente, come «Prima del backup del…»: la cartella di una
    /// versione contiene le copie di ciò che quel backup ha sostituito o cancellato, cioè lo stato
    /// di prima (vedi <see cref="RestorePlanner"/>).
    /// <para>Si elencano TUTTI i punti, anche quelli di solo manifest: ogni punto è uno stato
    /// diverso, e togliere il più vecchio renderebbe irraggiungibili proprio le copie di file
    /// cancellati dalla sorgente che stanno solo lì. Nessuna versione ancora (job appena acceso,
    /// destinazione vuota): elenco vuoto, e resta solo «Adesso».</para>
    /// </summary>
    private static List<RestorePointItem> BuildPoints(string dest)
    {
        if (dest.Length == 0) return new List<RestorePointItem>();
        var before = Loc.Instance["Restore_PointBefore"];
        var versions = VersioningLayout.VersionsDir(dest);
        return VersionCatalog.List(versions).OrderByDescending(p => p.Date).Select(p =>
        {
            var label = string.Format(before, p.Date.ToString("g"));
            if (p.HasFolder)
            {
                // Quanti file quel backup ha sostituito o cancellato: è l'unico numero che aiuti a
                // riconoscere «il giorno in cui è successo il pasticcio». Un punto di solo
                // manifest non ne ha sostituito nessuno, e lo zero sarebbe solo rumore.
                var manifest = VersionManifest.ReadFrom(Path.Combine(versions, p.Name));
                var changed = (manifest?.Changed.Count ?? 0) + (manifest?.Deleted.Count ?? 0);
                if (changed > 0) label += " — " + string.Format(Loc.Instance["Restore_PointChanges"], changed);
            }
            return new RestorePointItem { Label = label, Date = p.Date, Name = p.Name };
        }).ToList();
    }

    /// <summary>Ricostruisce l'albero del momento scelto.</summary>
    public async Task LoadPlanAsync()
    {
        if (SelectedPoint is not { } point) return;
        IsBusy = true;
        IsDone = false;
        Failures = "";
        SetStatus(Loc.Instance["Restore_Loading"]);
        try
        {
            var dest = (_job.Destination ?? "").Trim();
            var when = point.Date;
            // Anche l'indice dell'albero si costruisce qui, sul thread di lavoro: su una
            // destinazione da centomila file è la stessa mole di lavoro della risoluzione, e
            // farlo sul thread della UI congelerebbe la finestra a ogni cambio di data.
            // RestoreTree non tocca nessun oggetto WPF, quindi può nascere fuori dalla UI.
            var onlyChanged = _onlyChanged && point.Name is not null;
            var (plan, tree) = await Task.Run(() =>
            {
                var p = RestorePlanner.Resolve(dest, when);
                // «Solo i file cambiati»: il piano completo filtrato con il manifest di quel
                // backup. Senza manifest leggibile resta la vista completa, non una vista vuota.
                if (onlyChanged && VersionManifest.ReadFrom(
                        Path.Combine(VersioningLayout.VersionsDir(dest), point.Name!)) is { } manifest)
                    p = RestorePlanner.OnlyChangedBy(p, manifest);
                return (p, new RestoreTree(p));
            });

            _plan = plan;
            _tree = tree;
            // L'aggancio si fa QUI, sul thread della UI: la notifica riaccende un pulsante.
            _tree.SelectionChanged += OnSelectionChanged;
            ShowNodes();

            Totals = string.Format(Loc.Instance["Restore_Totals"],
                _plan.Files.Count, VersionsUsage.Describe(_plan.TotalBytes));
            IsEmpty = _plan.Files.Count == 0;
            SetStatus("");
            CheckTarget();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, warning: true);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanRestoreAll));
            OnPropertyChanged(nameof(CanRestoreSelection));
        }
    }

    private void OnSelectionChanged()
    {
        OnPropertyChanged(nameof(CanRestoreSelection));
        OnPropertyChanged(nameof(AllChecked));
    }

    /// <summary>La casella dell'intestazione: spuntata se è spuntato tutto ciò che è a video
    /// (l'albero intero o, con la ricerca attiva, i risultati), vuota se niente, «a metà» altrimenti.</summary>
    public bool? AllChecked
    {
        get
        {
            if (_tree is not { } tree || Nodes.Count == 0) return false;
            bool anyOn = false, anyOff = false;
            foreach (var node in Nodes)
            {
                switch (tree.StateOf(node.RelativePath, node.IsFolder))
                {
                    case true: anyOn = true; break;
                    case false: anyOff = true; break;
                    default: return null;
                }
                if (anyOn && anyOff) return null;
            }
            return anyOn;
        }
    }

    /// <summary>Spunta o toglie la spunta a tutto. Con la ricerca attiva vale per i soli risultati
    /// a video (è ciò che l'utente sta guardando); senza, per l'albero intero, rami chiusi compresi.</summary>
    public void SelectAll(bool value)
    {
        if (_tree is not { } tree) return;
        if (_search.Trim().Length > 0)
            foreach (var node in Nodes) tree.Set(node.RelativePath, value);
        else
            tree.SetAll(value);
        foreach (var node in Nodes) node.Refresh();
    }

    /// <summary>Riempie l'elenco a video: l'albero vero, oppure — con la ricerca attiva — i soli
    /// file il cui nome corrisponde, in elenco piatto con il percorso intero.</summary>
    private void ShowNodes()
    {
        FillNodes();
        OnPropertyChanged(nameof(AllChecked)); // la casella dell'intestazione dipende da cosa è a video
    }

    private void FillNodes()
    {
        Nodes.Clear();
        if (_tree is not { } tree) return;

        var needle = _search.Trim();
        if (needle.Length == 0)
        {
            foreach (var item in tree.ChildrenOf("")) Nodes.Add(new RestoreNode(tree, null, item));
            return;
        }

        // Massimo 500 risultati: oltre, un elenco a video non si legge più e costa solo memoria.
        foreach (var (rel, entry) in _plan.Files
                     .Where(kv => Path.GetFileName(kv.Key).Contains(needle, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                     .Take(500))
        {
            Nodes.Add(new RestoreNode(tree, null,
                new RestoreTree.Item(Path.GetFileName(rel), rel, IsFolder: false, entry.Size), label: rel));
        }
    }

    /// <summary>Avvisi sulla cartella scelta: la sorgente del job è vietata, una cartella non
    /// vuota è solo da sapere (niente viene sovrascritto).</summary>
    private void CheckTarget()
    {
        var target = _target.Trim();
        if (target.Length == 0) { SetStatus(""); return; }

        if (RestoreCopier.IsSourceOrInside(target, _job.Source))
        {
            SetStatus(Loc.Instance["Restore_TargetIsSource"], warning: true);
            return;
        }
        if (RestoreCopier.TouchesBackup(target, _job.Destination))
        {
            SetStatus(Loc.Instance["Restore_TargetIsBackup"], warning: true);
            return;
        }

        // Una volta sola per cartella: ripeterlo a ogni tasto premuto lo renderebbe invisibile.
        if (_warnedTarget.Equals(target, StringComparison.OrdinalIgnoreCase)) return;
        _warnedTarget = target;
        try
        {
            if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
                SetStatus(Loc.Instance["Restore_TargetNotEmpty"], warning: true);
            else
                SetStatus("");
        }
        catch { SetStatus(""); }
    }

    /// <summary>Il piano intero: tutto com'era alla data scelta.</summary>
    public RestorePlan FullPlan => _plan;

    /// <summary>Il piano dei soli percorsi spuntati.</summary>
    public RestorePlan SelectedPlan =>
        _tree is { } tree ? RestorePlanner.Select(_plan, tree.Selection()) : RestorePlan.Empty;

    /// <summary>Copia il piano indicato nella cartella scelta, con avanzamento e annullamento.</summary>
    public async Task RestoreAsync(RestorePlan plan)
    {
        if (IsBusy) return;
        IsBusy = true;
        IsCopying = true;
        IsDone = false;
        Failures = "";
        Progress = 0;
        ProgressMax = Math.Max(1, plan.Files.Count);
        SetStatus(string.Format(Loc.Instance["Restore_Copying"], 0, plan.Files.Count));

        var target = _target.Trim();
        _cts = new CancellationTokenSource();
        try
        {
            var progress = new Progress<(int Done, int Total)>(p =>
            {
                Progress = p.Done;
                SetStatus(string.Format(Loc.Instance["Restore_Copying"], p.Done, p.Total));
            });
            var outcome = await RestoreCopier.CopyAsync(plan, target, _job.Source, progress, _cts.Token,
                jobDestination: _job.Destination);

            RestoredTo = target;
            LastRestoreClean = !outcome.Cancelled && outcome.Failures.Count == 0;
            // Un ripristino riuscito non è un avviso: il giallo è riservato a ciò che è rimasto
            // indietro (annullamento) o non ha funzionato.
            if (outcome.Cancelled)
                SetStatus(string.Format(Loc.Instance["Restore_DoneCancelled"], outcome.Copied, outcome.Total),
                    warning: true);
            else
                SetStatus(string.Format(Loc.Instance["Restore_Done"], outcome.Copied, outcome.Total,
                    VersionsUsage.Describe(outcome.Bytes)));
            if (outcome.Failures.Count > 0)
            {
                // I primi venti bastano a capire che cosa è successo; il conto completo è nel titolo.
                Failures = string.Format(Loc.Instance["Restore_Failures"], outcome.Failures.Count)
                    + Environment.NewLine
                    + string.Join(Environment.NewLine,
                        outcome.Failures.Take(20).Select(f => $"• {f.RelativePath} — {f.Error}"));
            }
            IsDone = true;
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, warning: true);
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            IsCopying = false;
            IsBusy = false;
        }
    }

    /// <summary>Ferma la copia in corso: i file già arrivati restano dove sono.</summary>
    public void Cancel() => _cts?.Cancel();
}
