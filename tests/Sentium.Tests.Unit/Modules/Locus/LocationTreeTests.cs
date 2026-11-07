using FluentAssertions;
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

    private static LocationTree CreateTree() => new(
    [
        new LocationNode(Home, null, "Home"),
        new LocationNode(Garage, Home, "Garage"),
        new LocationNode(Shelf, Garage, "Shelf B"),
        new LocationNode(Kitchen, Home, "Kitchen"),
        new LocationNode(Cabin, null, "Cabin"),
    ]);

    [Fact]
    public void GetPath_ReturnsRootFirstCrumbs_AndReadablePath()
    {
        var tree = CreateTree();

        tree.GetPath(Shelf).Select(c => c.Name).Should().Equal("Home", "Garage", "Shelf B");
        tree.GetPathString(Shelf).Should().Be("Home › Garage › Shelf B");
        tree.GetDepth(Shelf).Should().Be(3);
        tree.GetDepth(Home).Should().Be(1);
    }

    [Fact]
    public void GetPath_ForUnknownId_IsEmpty()
    {
        CreateTree().GetPath(Guid.NewGuid()).Should().BeEmpty();
    }

    [Fact]
    public void Roots_AndChildren_AreSortedByName()
    {
        var tree = CreateTree();

        tree.Roots.Select(r => r.Name).Should().Equal("Cabin", "Home");
        tree.ChildrenOf(Home).Select(c => c.Name).Should().Equal("Garage", "Kitchen");
    }

    [Fact]
    public void GetSubtreeIds_IncludesSelfAndAllDescendants()
    {
        CreateTree().GetSubtreeIds(Home).Should().BeEquivalentTo([Home, Garage, Shelf, Kitchen]);
        CreateTree().GetSubtreeIds(Shelf).Should().Equal(Shelf);
    }

    [Fact]
    public void GetHeight_CountsLevelsBelow()
    {
        var tree = CreateTree();

        tree.GetHeight(Home).Should().Be(2);
        tree.GetHeight(Garage).Should().Be(1);
        tree.GetHeight(Shelf).Should().Be(0);
    }

    [Fact]
    public void ValidatePlacement_RejectsMovingIntoOwnSubtree()
    {
        var tree = CreateTree();

        tree.ValidatePlacement(Home, Shelf, 12).Should().NotBeNull();
        tree.ValidatePlacement(Garage, Garage, 12).Should().NotBeNull();
        tree.ValidatePlacement(Garage, Cabin, 12).Should().BeNull();
        tree.ValidatePlacement(Garage, null, 12).Should().BeNull();
    }

    [Fact]
    public void ValidatePlacement_EnforcesMaximumDepth_IncludingTheMovedSubtree()
    {
        var tree = CreateTree();

        // Garage (height 1) under Shelf (depth 3) would reach depth 5.
        tree.ValidatePlacement(Kitchen, Shelf, 4).Should().BeNull();
        // Garage under Kitchen (depth 2) becomes depth 3 and its Shelf depth 4.
        tree.ValidatePlacement(Garage, Kitchen, 4).Should().BeNull();
        tree.ValidatePlacement(Garage, Kitchen, 3).Should().Contain("at most 3");
        tree.ValidatePlacement(null, Shelf, 3).Should().Contain("at most 3");
        tree.ValidatePlacement(null, Shelf, 4).Should().BeNull();
    }

    [Fact]
    public void CyclicData_NeverLoopsForever()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var tree = new LocationTree([new LocationNode(a, b, "A"), new LocationNode(b, a, "B")]);

        tree.GetPath(a).Should().HaveCount(2);
        tree.GetSubtreeIds(a).Should().BeEquivalentTo([a, b]);
        tree.Roots.Should().BeEmpty();
        tree.GetHeight(a).Should().BeLessThan(3);
        tree.IsSelfOrDescendant(b, a).Should().BeTrue();
    }

    [Fact]
    public void Resolve_MatchesIdPathSuffixExactAndPartialNames()
    {
        var tree = CreateTree();

        tree.Resolve(Shelf.ToString()).Should().ContainSingle().Which.Id.Should().Be(Shelf);
        tree.Resolve("Garage/Shelf B").Should().ContainSingle().Which.Id.Should().Be(Shelf);
        tree.Resolve("home > garage").Should().ContainSingle().Which.Id.Should().Be(Garage);
        tree.Resolve("Home › Garage › Shelf B").Should().ContainSingle().Which.Id.Should().Be(Shelf);
        tree.Resolve("kitchen").Should().ContainSingle().Which.Id.Should().Be(Kitchen);
        tree.Resolve("shel").Should().ContainSingle().Which.Id.Should().Be(Shelf);
        tree.Resolve("Attic").Should().BeEmpty();
        tree.Resolve("  ").Should().BeEmpty();
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

        tree.Resolve("garage").Should().ContainSingle().Which.Id.Should().Be(Garage);
    }

    [Fact]
    public void FindSubtreesMatching_ReturnsMatchingLocationsAndTheirDescendants()
    {
        CreateTree().FindSubtreesMatching("gar").Should().BeEquivalentTo([Garage, Shelf]);
    }
}
