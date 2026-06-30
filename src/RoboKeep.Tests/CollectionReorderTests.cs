using System.Collections.ObjectModel;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class CollectionReorderTests
{
    // Tipo a identita di riferimento (Equals non sovrascritto): IndexOf usa la reference,
    // cosi possiamo verificare che gli elementi siano gli STESSI oggetti, solo riordinati.
    private sealed class Item
    {
        public Item(string name) => Name = name;
        public string Name { get; }
    }

    [Fact]
    public void SortByKey_Ascending_OrdersAndPreservesInstances()
    {
        var a = new Item("Charlie");
        var b = new Item("alpha");
        var c = new Item("Bravo");
        var coll = new ObservableCollection<Item> { a, b, c };

        CollectionReorder.SortByKey(coll, x => x.Name, ascending: true);

        Assert.Equal(new[] { "alpha", "Bravo", "Charlie" }, coll.Select(x => x.Name)); // case-insensitive
        Assert.Same(b, coll[0]); // stesse istanze, non ricreate
        Assert.Same(c, coll[1]);
        Assert.Same(a, coll[2]);
    }

    [Fact]
    public void SortByKey_Descending_OrdersReversed()
    {
        var coll = new ObservableCollection<Item> { new("alpha"), new("Charlie"), new("Bravo") };

        CollectionReorder.SortByKey(coll, x => x.Name, ascending: false);

        Assert.Equal(new[] { "Charlie", "Bravo", "alpha" }, coll.Select(x => x.Name));
    }

    [Fact]
    public void SortByKey_IsStable_ForEqualKeys()
    {
        var first = new Item("same");
        var second = new Item("same");
        var coll = new ObservableCollection<Item> { first, second };

        CollectionReorder.SortByKey(coll, x => x.Name, ascending: true);

        Assert.Same(first, coll[0]); // ordine relativo preservato
        Assert.Same(second, coll[1]);
    }

    [Fact]
    public void SortByKey_AlreadySorted_NoChange()
    {
        var coll = new ObservableCollection<Item> { new("a"), new("b"), new("c") };
        var moved = 0;
        coll.CollectionChanged += (_, e) => { if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Move) moved++; };

        CollectionReorder.SortByKey(coll, x => x.Name, ascending: true);

        Assert.Equal(0, moved); // nessuno spostamento superfluo
    }
}
