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
        Assert.Equal(RootNote, Resolver.Resolve("ideas"));
    }

    [Fact]
    public void APartialPath_MatchesTheNoteEndingWithIt()
    {
        Assert.Equal(DeepNote, Resolver.Resolve("sentium/ideas"));
    }

    [Fact]
    public void AnExactPath_BeatsShorterSuffixMatches()
    {
        Assert.Equal(OtherDeepNote, Resolver.Resolve("archive/ideas"));
    }

    [Fact]
    public void AnAlias_ResolvesWhenNoPathMatches()
    {
        Assert.Equal(AliasedNote, Resolver.Resolve("countess"));
    }

    [Fact]
    public void AnUnknownTarget_StaysUnresolved()
    {
        Assert.Null(Resolver.Resolve("missing"));
        Assert.Null(Resolver.Resolve("other/countess"));
    }

    [Fact]
    public void KeysAffectedBy_CoversEveryPathSuffixAndAlias()
    {
        Assert.Equal(new string[] { "a/b/c", "b/c", "c", "alias" }, LinkResolver.KeysAffectedBy("a/b/c", ["alias"]));
    }
}
