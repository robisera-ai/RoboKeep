using System.Collections.ObjectModel;

namespace RoboKeep.Core.Services;

/// <summary>
/// Riordino sul posto di una ObservableCollection. Usato per l'ordinamento per intestazione nella
/// griglia dei job: invece di applicare un ordinamento di VISTA (che avrebbe la precedenza sul vero
/// ordine della collezione e nasconderebbe il riordino manuale via drag &amp; drop), riordina
/// FISICAMENTE la collezione. Cosi l'ordine visibile coincide sempre con quello reale/salvato.
/// </summary>
public static class CollectionReorder
{
    /// <summary>
    /// Ordina la collezione per una chiave testuale, con il minimo numero di spostamenti e
    /// preservando l'identita degli elementi (quindi selezione e binding restano agganciati).
    /// L'ordinamento e stabile: elementi con chiave uguale mantengono l'ordine relativo.
    /// </summary>
    public static void SortByKey<T>(ObservableCollection<T> items, Func<T, string> key, bool ascending, StringComparer? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(key);
        comparer ??= StringComparer.CurrentCultureIgnoreCase;

        var ordered = ascending
            ? items.OrderBy(key, comparer).ToList()
            : items.OrderByDescending(key, comparer).ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            var current = items.IndexOf(ordered[i]);
            if (current != i)
                items.Move(current, i);
        }
    }
}
