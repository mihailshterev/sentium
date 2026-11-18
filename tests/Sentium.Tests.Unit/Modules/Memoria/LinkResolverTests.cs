using FluentAssertions;
using Sentium.Memoria.Features.Links;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Memoria;

public sealed class LinkResolverTests
{
    private static readonly Guid RootNote = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid DeepNote = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid OtherDeepNote = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid AliasedNote = Guid.Parse("00000000-0000-0000-0000-000000000004");

    private static readonly LinkResolver Resolver = new(
    [
        new NoteKey(RootNote, "ideas", []),
        new NoteKey(DeepNote, "projects/sentium/ideas", []),
        new NoteKey(OtherDeepNote, "archive/ideas", []),
        new NoteKey(AliasedNote, "people/ada lovelace", ["ada", "countess"])
    ]);

    [Fact]
    public void ABareName_PrefersTheShortestPath()
    {
        Resolver.Resolve("ideas").Should().Be(RootNote);
    }

    [Fact]
    public void APartialPath_MatchesTheNoteEndingWithIt()
    {
        Resolver.Resolve("sentium/ideas").Should().Be(DeepNote);
    }

    [Fact]
    public void AnExactPath_BeatsShorterSuffixMatches()
    {
        Resolver.Resolve("archive/ideas").Should().Be(OtherDeepNote);
    }

    [Fact]
    public void AnAlias_ResolvesWhenNoPathMatches()
    {
        Resolver.Resolve("countess").Should().Be(AliasedNote);
    }

    [Fact]
    public void AnUnknownTarget_StaysUnresolved()
    {
        Resolver.Resolve("missing").Should().BeNull();
        Resolver.Resolve("other/countess").Should().BeNull();
    }

    [Fact]
    public void KeysAffectedBy_CoversEveryPathSuffixAndAlias()
    {
        LinkResolver.KeysAffectedBy("a/b/c", ["alias"]).Should().Equal("a/b/c", "b/c", "c", "alias");
    }
}
