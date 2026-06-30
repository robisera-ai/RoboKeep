using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using H.NotifyIcon.Core;
using RoboKeep.Core.Models;
using RoboKeep.Infra;
using RoboKeep.Localization;
using RoboKeep.ViewModels;

namespace RoboKeep;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly AppHost _host;
    private readonly MainViewModel _vm;

    // Tray: ultimo stato non minimizzato (per ripristinarlo) e flag di uscita in corso.
    private WindowState _restoreState = WindowState.Normal;
    private bool _exiting;
    private bool _trayHintShown;

    // Stato per il drag &amp; drop di riordino dei job (solo dalla maniglia).
    private JobViewModel? _dragItem;
    private Point _dragStart;
    private DragAdorner? _adorner;
    private AdornerLayer? _adornerLayer;
    private InsertionAdorner? _insertAdorner;
    private DataGridRow? _insertRow;
    private bool _insertBelow;

    public MainWindow()
    {
        _host = AppHost.Load();
        Loc.Instance.ApplyFromSetting(_host.Config.Settings.Language);
        InitializeComponent();
        _vm = new MainViewModel(_host);
        DataContext = _vm;

        // Il log viene riversato a blocchi (sul thread UI) per non ingolfare l'interfaccia.
        _vm.LogFlushed += text =>
        {
            LogBox.AppendText(text);
            LogBox.ScrollToEnd();
        };
        _vm.LogCleared += () => LogBox.Clear();

        // Avvia minimizzato nel tray se richiesto dalle impostazioni.
        if (_host.Config.Settings.StartMinimized)
        {
            WindowState = System.Windows.WindowState.Minimized;
            // Hide() qui non basta: l'app mostra la finestra dopo il costruttore, lasciando
            // il pulsante nella barra delle applicazioni. Nascondiamo a finestra gia mostrata.
            if (_host.Config.Settings.MinimizeToTray)
                Loaded += OnLoadedHideToTray;
        }
    }

    // Avvio minimizzato: nasconde nel tray dopo che la finestra e stata mostrata,
    // cosi non resta il pulsante nella barra delle applicazioni.
    private void OnLoadedHideToTray(object sender, System.Windows.RoutedEventArgs e)
    {
        Loaded -= OnLoadedHideToTray;
        Hide();
        // Avviso "partito nel tray" con un piccolo ritardo: chiamare ShowNotification durante
        // Loaded destabilizza l'avvio (H.NotifyIcon); a regime e' sicuro come il toast di fine job.
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = System.TimeSpan.FromSeconds(2) };
        timer.Tick += (s, _) =>
        {
            timer.Stop();
            if (_trayHintShown) return;
            _trayHintShown = true;
            TrayIcon.ShowNotification(
                Loc.Instance["Tray_StartedTitle"],
                Loc.Instance["Tray_StartedBody"],
                NotificationIcon.Info);
        };
        timer.Start();
    }

    // Con "riduci nel tray" attivo, la X nasconde nel tray invece di chiudere.
    // L'uscita vera passa da tray -> Esci, che imposta _exiting prima di Shutdown.
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_exiting && _host.Config.Settings.MinimizeToTray)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        base.OnClosing(e);
    }

    // Alla chiusura della finestra distrugge l'icona del tray: evita che resti un "fantasma"
    // nell'area di notifica dopo la chiusura normale.
    protected override void OnClosed(System.EventArgs e)
    {
        _exiting = true;
        TrayIcon.Dispose();
        base.OnClosed(e);
    }

    // Minimizza nel tray invece di mostrare la barra delle applicazioni (se abilitato).
    protected override void OnStateChanged(System.EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState != System.Windows.WindowState.Minimized)
            _restoreState = WindowState;                 // ricorda l'ultimo stato non minimizzato
        else if (_host.Config.Settings.MinimizeToTray && IsVisible)
            HideToTray();                                 // IsVisible evita il doppio Hide all'avvio
    }

    // Nasconde la finestra nel tray e, solo la prima volta nella sessione, avvisa che l'app resta attiva.
    private void HideToTray()
    {
        Hide();
        if (_trayHintShown) return;
        _trayHintShown = true;
        TrayIcon.ShowNotification(
            Loc.Instance["Tray_StillRunningTitle"],
            Loc.Instance["Tray_StillRunningBody"],
            NotificationIcon.Info);
    }

    // ----- Gestori icona nel tray -----

    private void OnTrayShow(object sender, System.EventArgs e)
    {
        Show();
        WindowState = _restoreState;                      // ripristina Normal o Maximized, non forza Normal
        Activate();
    }

    private void OnTrayRunAll(object sender, System.Windows.RoutedEventArgs e)
        => _vm.RunAllCommand.Execute(null);

    private void OnTrayExit(object sender, System.Windows.RoutedEventArgs e)
    {
        _exiting = true;
        System.Windows.Application.Current.Shutdown();    // OnClosed distrugge l'icona (no doppio dispose)
    }

    /// <summary>Mostra una notifica toast tramite l'icona del tray.</summary>
    internal void ShowJobToast(string title, string message)
    {
        if (_exiting) return;
        TrayIcon.ShowNotification(title, message, NotificationIcon.Info);
    }

    // Ordinamento per intestazione: riordina FISICAMENTE la collezione (vedi MainViewModel.SortJobs)
    // invece di applicare un ordinamento di vista. Così l'ordine visibile coincide sempre con quello
    // reale e il riordino manuale via drag & drop resta visibile (un ordinamento di vista lo nasconderebbe).
    private void OnGridSorting(object sender, DataGridSortingEventArgs e)
    {
        var path = e.Column.SortMemberPath;
        if (string.IsNullOrEmpty(path)) return;

        // Toggle: se la colonna era già crescente passa a decrescente, altrimenti crescente.
        var direction = e.Column.SortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;

        _vm.SortJobs(path, direction == ListSortDirection.Ascending);

        // Gestiamo a mano la freccetta: solo la colonna attiva la mostra.
        foreach (var c in JobsGrid.Columns) c.SortDirection = null;
        e.Column.SortDirection = direction;
        e.Handled = true; // niente ordinamento di vista: l'ordine fisico è già stato applicato
    }

    // ----- Riordino dei job via drag & drop -----

    // Il drag parte SOLO dalla maniglia (≡), non dall'intera riga.
    private void OnDragHandleMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragItem = (sender as FrameworkElement)?.DataContext as JobViewModel;
    }

    // Permette di DESELEZIONARE: un clic su una riga già selezionata la deseleziona,
    // e un clic nell'area vuota azzera la selezione. Senza interferire con doppio clic
    // (modifica), trascinamento dalla maniglia e switch Attivo.
    private void OnGridPreviewLeftDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 1) return; // lascia passare il doppio clic (modifica)

        var src = e.OriginalSource as DependencyObject;

        // I clic sulle intestazioni di colonna non toccano la selezione (servono all'ordinamento).
        if (FindAncestor<System.Windows.Controls.Primitives.DataGridColumnHeader>(src) is not null)
            return;

        var row = FindAncestor<DataGridRow>(src);
        if (row is null)
        {
            JobsGrid.SelectedItem = null; // clic nell'area vuota
            return;
        }

        if (!row.IsSelected) return; // selezione normale di una riga non ancora selezionata

        // Non deselezionare se il clic è sulla maniglia (col. 0) o sullo switch Attivo (col. 1),
        // che hanno una loro interazione.
        var cell = FindAncestor<DataGridCell>(src);
        if (cell is not null && JobsGrid.Columns.Count >= 2 &&
            (cell.Column == JobsGrid.Columns[0] || cell.Column == JobsGrid.Columns[1]))
            return;

        JobsGrid.SelectedItem = null; // riga già selezionata: il clic la deseleziona
        e.Handled = true;
    }

    private void OnGridMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragItem is null)
            return;

        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        // "Fantasma" semitrasparente della riga trascinata.
        if (JobsGrid.ItemContainerGenerator.ContainerFromItem(_dragItem) is DataGridRow row)
        {
            _adornerLayer = AdornerLayer.GetAdornerLayer(JobsGrid);
            if (_adornerLayer is not null)
            {
                _adorner = new DragAdorner(JobsGrid, row, row.RenderSize);
                _adornerLayer.Add(_adorner);
            }
        }

        try
        {
            DragDrop.DoDragDrop(JobsGrid, _dragItem, DragDropEffects.Move);
        }
        finally
        {
            if (_adorner is not null && _adornerLayer is not null)
                _adornerLayer.Remove(_adorner);
            ClearInsertion();
            _adorner = null;
            _adornerLayer = null;
            _dragItem = null;
        }
    }

    private void OnGridDragOver(object sender, DragEventArgs e)
    {
        e.Effects = _dragItem is not null ? DragDropEffects.Move : DragDropEffects.None;
        if (_dragItem is not null)
        {
            var p = e.GetPosition(JobsGrid);
            _adorner?.SetPosition(p.X + 10, p.Y - 6);
            UpdateInsertion(p);
        }
        e.Handled = true;
    }

    private void OnGridDrop(object sender, DragEventArgs e)
    {
        // _dragItem è ancora valido qui (DoDragDrop è modale); viene azzerato nel finally.
        if (_dragItem is not null && _insertRow?.Item is JobViewModel target)
        {
            _vm.MoveJobToGap(_dragItem, target, _insertBelow);
            // Un riordino manuale rompe l'ordine per colonna: la freccetta non sarebbe più veritiera.
            foreach (var c in JobsGrid.Columns) c.SortDirection = null;
        }
    }

    // Aggiorna la linea di inserimento in base alla riga e alla metà (sopra/sotto) sotto il cursore.
    private void UpdateInsertion(Point gridPoint)
    {
        var row = FindAncestor<DataGridRow>(JobsGrid.InputHitTest(gridPoint) as DependencyObject);
        if (row is null) { ClearInsertion(); return; }

        var rel = JobsGrid.TranslatePoint(gridPoint, row);
        var below = rel.Y > row.ActualHeight / 2;
        if (ReferenceEquals(row, _insertRow) && below == _insertBelow) return;

        ClearInsertion();
        var layer = AdornerLayer.GetAdornerLayer(row);
        if (layer is null) return;
        _insertRow = row;
        _insertBelow = below;
        _insertAdorner = new InsertionAdorner(row, below);
        layer.Add(_insertAdorner);
    }

    private void ClearInsertion()
    {
        if (_insertAdorner is not null && _insertRow is not null)
            AdornerLayer.GetAdornerLayer(_insertRow)?.Remove(_insertAdorner);
        _insertAdorner = null;
        _insertRow = null;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null and not T)
            current = VisualTreeHelper.GetParent(current);
        return current as T;
    }

    private void OnNewJob(object sender, RoutedEventArgs e)
    {
        // Creazione guidata; "Salta" restituisce ResultJob = null -> editor vuoto come prima.
        var wizard = new JobWizardWindow { Owner = this };
        if (wizard.ShowDialog() != true)
            return;

        var job = wizard.ResultJob ?? new BackupJob { Name = Loc.Instance["Editor_NewJobName"] };
        if (ShowEditor(job))
        {
            _vm.Jobs.Add(new JobViewModel(job));
            _vm.PersistJobs();
        }
    }

    private void OnEditJob(object sender, RoutedEventArgs e)
    {
        var selected = _vm.SelectedJob;
        if (selected is null) return;

        // Modifica su una copia: se l'utente annulla, l'originale resta intatto.
        var clone = Clone(selected.Model);
        if (ShowEditor(clone))
        {
            CopyInto(clone, selected.Model);
            selected.RefreshAll();
            _vm.PersistJobs();
        }
    }

    private void OnDeleteJob(object sender, RoutedEventArgs e)
    {
        var selected = _vm.SelectedJob;
        if (selected is null) return;

        var confirm = MessageBox.Show(
            string.Format(Loc.Instance["Delete_Confirm"], selected.Name),
            Loc.Instance["Common_Confirm"],
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm == MessageBoxResult.Yes)
        {
            _vm.Jobs.Remove(selected);
            _vm.PersistJobs();
        }
    }

    private void OnBrowseVersions(object sender, RoutedEventArgs e)
    {
        var selected = _vm.SelectedJob;
        if (selected is null) return;
        var job = _host.Config.Jobs.FirstOrDefault(j => j.Name == selected.Name);
        if (job is null || !job.Versioned) return;

        var win = new SnapshotsWindow(job.Name, job.Destination) { Owner = this };
        win.ShowDialog();
    }

    private void OnSettings(object sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow(_host) { Owner = this };
        if (win.ShowDialog() == true)
            _host.SaveConfig();
    }

    private bool ShowEditor(BackupJob job)
    {
        var win = new JobEditorWindow(job, _host.Config.Credentials) { Owner = this };
        return win.ShowDialog() == true;
    }

    private static BackupJob Clone(BackupJob j) => new()
    {
        Name = j.Name,
        Source = j.Source,
        Destination = j.Destination,
        Mirror = j.Mirror,
        ExcludeOlder = j.ExcludeOlder,
        CopyAll = j.CopyAll,
        MultiThread = j.MultiThread,
        UnbufferedIO = j.UnbufferedIO,
        Restartable = j.Restartable,
        LogAllFiles = j.LogAllFiles,
        ExcludeFiles = new List<string>(j.ExcludeFiles),
        ExcludeDirs = new List<string>(j.ExcludeDirs),
        ForceCopyFiles = new List<string>(j.ForceCopyFiles),
        ForceCopySmart = j.ForceCopySmart,
        Retries = j.Retries,
        Wait = j.Wait,
        Enabled = j.Enabled,
        CredentialId = j.CredentialId,
        Versioned = j.Versioned,
        SnapshotKeepCount = j.SnapshotKeepCount,
        SnapshotMaxAgeDays = j.SnapshotMaxAgeDays,
    };

    private static void CopyInto(BackupJob from, BackupJob to)
    {
        to.Name = from.Name;
        to.Source = from.Source;
        to.Destination = from.Destination;
        to.Mirror = from.Mirror;
        to.ExcludeOlder = from.ExcludeOlder;
        to.CopyAll = from.CopyAll;
        to.MultiThread = from.MultiThread;
        to.UnbufferedIO = from.UnbufferedIO;
        to.Restartable = from.Restartable;
        to.LogAllFiles = from.LogAllFiles;
        to.ExcludeFiles = new List<string>(from.ExcludeFiles);
        to.ExcludeDirs = new List<string>(from.ExcludeDirs);
        to.ForceCopyFiles = new List<string>(from.ForceCopyFiles);
        to.ForceCopySmart = from.ForceCopySmart;
        to.Retries = from.Retries;
        to.Wait = from.Wait;
        to.Enabled = from.Enabled;
        to.CredentialId = from.CredentialId;
        to.Versioned = from.Versioned;
        to.SnapshotKeepCount = from.SnapshotKeepCount;
        to.SnapshotMaxAgeDays = from.SnapshotMaxAgeDays;
    }
}
