using RoboKeep.Core.Services;
using RoboKeep.ViewModels;

namespace RoboKeep.Tests;

/// <summary>
/// Le caselle dell'albero di ripristino: spuntare una cartella prende tutto ciò che c'è sotto
/// (anche i rami mai aperti), e un'eccezione fatta al suo interno viene rispettata. È la parte che
/// decide che cosa viene copiato davvero, quindi non può vivere solo dentro la finestra.
/// </summary>
public class RestoreTreeTests
{
    private static RestorePlan Plan(params string[] relativePaths)
    {
        var files = relativePaths.ToDictionary(
            r => r, r => new RestoreEntry(r, @"D:\x\" + r, 10, null), StringComparer.OrdinalIgnoreCase);
        var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rel in relativePaths)
        {
            var d = Path.GetDirectoryName(rel);
            while (!string.IsNullOrEmpty(d)) { dirs.Add(d); d = Path.GetDirectoryName(d); }
        }
        return new RestorePlan(files, dirs.ToList(), files.Count * 10);
    }

    [Fact]
    public void TickingAFolder_SelectsItWholeWithASingleEntry()
    {
        var plan = Plan(@"foto\a.jpg", @"foto\2026\b.jpg", "fuori.txt");
        var tree = new RestoreTree(plan);

        tree.Set("foto", true);

        Assert.Equal(new[] { "foto" }, tree.Selection());
        Assert.Equal(2, RestorePlanner.Select(plan, tree.Selection()).Files.Count);
        Assert.True(tree.AnySelected);
    }

    [Fact]
    public void AnExceptionInsideAFolder_IsHonoured_AndTheFolderLooksPartlyTicked()
    {
        var plan = Plan(@"foto\a.jpg", @"foto\b.jpg", @"foto\2026\c.jpg");
        var tree = new RestoreTree(plan);

        tree.Set("foto", true);
        tree.Set(@"foto\b.jpg", false);

        Assert.Null(tree.StateOf("foto", isFolder: true));   // casella «a metà»
        var chosen = RestorePlanner.Select(plan, tree.Selection()).Files;
        Assert.True(chosen.ContainsKey(@"foto\a.jpg"));
        Assert.True(chosen.ContainsKey(@"foto\2026\c.jpg"));
        Assert.False(chosen.ContainsKey(@"foto\b.jpg"));
    }

    [Fact]
    public void TickingAFolderAgain_WipesTheExceptionsUnderIt()
    {
        var plan = Plan(@"foto\a.jpg", @"foto\b.jpg");
        var tree = new RestoreTree(plan);

        tree.Set("foto", true);
        tree.Set(@"foto\b.jpg", false);
        tree.Set("foto", true);

        Assert.True(tree.StateOf("foto", isFolder: true));
        Assert.Equal(2, RestorePlanner.Select(plan, tree.Selection()).Files.Count);
    }

    [Fact]
    public void ASingleFileDeepDown_IsTheOnlyThingSelected()
    {
        var plan = Plan(@"foto\2026\c.jpg", @"foto\a.jpg");
        var tree = new RestoreTree(plan);

        tree.Set(@"foto\2026\c.jpg", true);

        Assert.Equal(new[] { @"foto\2026\c.jpg" }, tree.Selection());
        // «foto» si mostra a metà (a.jpg no, c.jpg sì); «2026» contiene solo c.jpg, che è
        // spuntato, quindi è piena.
        Assert.Null(tree.StateOf("foto", isFolder: true));
        Assert.True(tree.StateOf(@"foto\2026", isFolder: true));
    }

    [Fact]
    public void AFolderWhoseFilesAreAllTickedOneByOne_LooksFullyTicked()
    {
        // Riscontro della prova a mano: spuntando i file uno per uno la cartella restava «a metà»
        // anche quando c'erano tutti.
        var plan = Plan(@"nuova\a.txt", @"nuova\b.txt", @"nuova\sotto\c.txt", "fuori.txt");
        var tree = new RestoreTree(plan);

        tree.Set(@"nuova\a.txt", true);
        Assert.Null(tree.StateOf("nuova", isFolder: true));

        tree.Set(@"nuova\b.txt", true);
        tree.Set(@"nuova\sotto\c.txt", true);
        Assert.True(tree.StateOf("nuova", isFolder: true));
        Assert.True(tree.StateOf(@"nuova\sotto", isFolder: true));

        tree.Set(@"nuova\a.txt", false);
        tree.Set(@"nuova\b.txt", false);
        tree.Set(@"nuova\sotto\c.txt", false);
        Assert.False(tree.StateOf("nuova", isFolder: true));
    }

    [Fact]
    public void NothingTicked_IsAnEmptySelection()
    {
        var tree = new RestoreTree(Plan(@"foto\a.jpg"));

        Assert.False(tree.AnySelected);
        Assert.Empty(tree.Selection());
    }

    [Fact]
    public void ChildrenAreListedFoldersFirst_ThenByName()
    {
        var plan = Plan("zeta.txt", "alfa.txt", @"mezzo\x.txt");
        var tree = new RestoreTree(plan);

        Assert.Equal(new[] { "mezzo", "alfa.txt", "zeta.txt" },
            tree.ChildrenOf("").Select(i => i.Name).ToArray());
    }
}
