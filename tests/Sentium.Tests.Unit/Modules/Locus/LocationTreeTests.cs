using Sentium.Locus.Features.Locations;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Locus;

public sealed class LocationTreeTests
{
    private static readonly Guid Home = Guid.NewGuid();
    private static readonly Guid Garage = Guid.NewGuid();
    private static readonly Guid Shelf = Guid.NewGuid();
    private static readonly Guid Kitchen = Guid.NewGuid();
    private static readonly Guid Cabin = Guid.NewGuid();

    private static LocationTree CreateTree()
    {
        return new(
            [
                new LocationNode(Home, null, "Home"),
                new LocationNode(Garage, Home, "Garage"),
                new LocationNode(Shelf, Garage, "Shelf B"),
                new LocationNode(Kitchen, Home, "Kitchen"),
                new LocationNode(Cabin, null, "Cabin"),
            ]);
    }

    [Fact]
    public void GetPath_ReturnsRootFirstCrumbs_AndReadablePath()
    {
        var tree = CreateTree();

        Assert.Equal(new string[] { "Home", "Garage", "Shelf B" }, tree.GetPath(Shelf).Select(c => c.Name));
        Assert.Equal("Home › Garage › Shelf B", tree.GetPathString(Shelf));
        Assert.Equal(3, tree.GetDepth(Shelf));
        Assert.Equal(1, tree.GetDepth(Home));
    }

    [Fact]
    public void GetPath_ForUnknownId_IsEmpty()
    {
        Assert.Empty(CreateTree().GetPath(Guid.NewGuid()));
    }

    [Fact]
    public void Roots_AndChildren_AreSortedByName()
    {
        var tree = CreateTree();

        Assert.Equal(new string[] { "Cabin", "Home" }, tree.Roots.Select(r => r.Name));
        Assert.Equal(new string[] { "Garage", "Kitchen" }, tree.ChildrenOf(Home).Select(c => c.Name));
    }

    [Fact]
    public void GetSubtreeIds_IncludesSelfAndAllDescendants()
    {
        Assert.Equivalent(new[] { Home, Garage, Shelf, Kitchen }, CreateTree().GetSubtreeIds(Home));
        Assert.Equal(new Guid[] { Shelf }, CreateTree().GetSubtreeIds(Shelf));
    }

    [Fact]
    public void GetHeight_CountsLevelsBelow()
    {
        var tree = CreateTree();

        Assert.Equal(2, tree.GetHeight(Home));
        Assert.Equal(1, tree.GetHeight(Garage));
        Assert.Equal(0, tree.GetHeight(Shelf));
    }

    [Fact]
    public void ValidatePlacement_RejectsMovingIntoOwnSubtree()
    {
        var tree = CreateTree();

        Assert.NotNull(tree.ValidatePlacement(Home, Shelf, 12));
        Assert.NotNull(tree.ValidatePlacement(Garage, Garage, 12));
        Assert.Null(tree.ValidatePlacement(Garage, Cabin, 12));
        Assert.Null(tree.ValidatePlacement(Garage, null, 12));
    }

    [Fact]
    public void ValidatePlacement_EnforcesMaximumDepth_IncludingTheMovedSubtree()
    {
        var tree = CreateTree();

        // Garage (height 1) under Shelf (depth 3) would reach depth 5.
        Assert.Null(tree.ValidatePlacement(Kitchen, Shelf, 4));
        // Garage under Kitchen (depth 2) becomes depth 3 and its Shelf depth 4.
        Assert.Null(tree.ValidatePlacement(Garage, Kitchen, 4));
        Assert.Contains("at most 3", tree.ValidatePlacement(Garage, Kitchen, 3), StringComparison.Ordinal);
        Assert.Contains("at most 3", tree.ValidatePlacement(null, Shelf, 3), StringComparison.Ordinal);
        Assert.Null(tree.ValidatePlacement(null, Shelf, 4));
    }

    [Fact]
    public void CyclicData_NeverLoopsForever()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var tree = new LocationTree([new LocationNode(a, b, "A"), new LocationNode(b, a, "B")]);

        Assert.Equal(2, tree.GetPath(a).Count);
        Assert.Equivalent(new[] { a, b }, tree.GetSubtreeIds(a));
        Assert.Empty(tree.Roots);
        Assert.True(tree.GetHeight(a) < 3);
        Assert.True(tree.IsSelfOrDescendant(b, a));
    }

    [Fact]
    public void Resolve_MatchesIdPathSuffixExactAndPartialNames()
    {
        var tree = CreateTree();

        Assert.Equal(Shelf, Assert.Single(tree.Resolve(Shelf.ToString())).Id);
        Assert.Equal(Shelf, Assert.Single(tree.Resolve("Garage/Shelf B")).Id);
        Assert.Equal(Garage, Assert.Single(tree.Resolve("home > garage")).Id);
        Assert.Equal(Shelf, Assert.Single(tree.Resolve("Home › Garage › Shelf B")).Id);
        Assert.Equal(Kitchen, Assert.Single(tree.Resolve("kitchen")).Id);
        Assert.Equal(Shelf, Assert.Single(tree.Resolve("shel")).Id);
        Assert.Empty(tree.Resolve("Attic"));
        Assert.Empty(tree.Resolve("  "));
    }

    [Fact]
    public void Resolve_PrefersExactNameOverPartialMatches()
    {
        var tree = new LocationTree(
        [
            new LocationNode(Home, null, "Home"),
            new LocationNode(Garage, Home, "Garage"),
            new LocationNode(Shelf, Home, "Garage cabinet"),
        ]);

        Assert.Equal(Garage, Assert.Single(tree.Resolve("garage")).Id);
    }

    [Fact]
    public void FindSubtreesMatching_ReturnsMatchingLocationsAndTheirDescendants()
    {
        Assert.Equivalent(new[] { Garage, Shelf }, CreateTree().FindSubtreesMatching("gar"));
    }
}
