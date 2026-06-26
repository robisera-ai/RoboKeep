using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using RobocopySW.Core.Models;
using RobocopySW.Infra;
using RobocopySW.Localization;
using RobocopySW.ViewModels;

namespace RobocopySW;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly AppHost _host;
    private readonly MainViewModel _vm;

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
    }

    // ----- Riordino dei job via drag & drop -----

    // Il drag parte SOLO dalla maniglia (≡), non dall'intera riga.
    private void OnDragHandleMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragItem = (sender as FrameworkElement)?.DataContext as JobViewModel;
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
            _vm.MoveJobToGap(_dragItem, target, _insertBelow);
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
        ExcludeFiles = new List<string>(j.ExcludeFiles),
        ExcludeDirs = new List<string>(j.ExcludeDirs),
        ForceCopyFiles = new List<string>(j.ForceCopyFiles),
        ForceCopySmart = j.ForceCopySmart,
        Retries = j.Retries,
        Wait = j.Wait,
        Enabled = j.Enabled,
        CredentialId = j.CredentialId,
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
        to.ExcludeFiles = new List<string>(from.ExcludeFiles);
        to.ExcludeDirs = new List<string>(from.ExcludeDirs);
        to.ForceCopyFiles = new List<string>(from.ForceCopyFiles);
        to.ForceCopySmart = from.ForceCopySmart;
        to.Retries = from.Retries;
        to.Wait = from.Wait;
        to.Enabled = from.Enabled;
        to.CredentialId = from.CredentialId;
    }
}
