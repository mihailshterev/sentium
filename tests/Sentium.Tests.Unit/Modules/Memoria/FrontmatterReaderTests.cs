using Sentium.Memoria.Features.Parsing;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Memoria;

public sealed class FrontmatterReaderTests
{
    [Fact]
    public void Read_AcceptsCommaSeparatedStringsAndHashPrefixes()
    {
        var frontmatter = FrontmatterReader.Read("tags: \"#one, two three\"\nalias: Solo");

        Assert.Equal(new string[] { "one", "two", "three" }, frontmatter.Tags);
        Assert.Equal(new string[] { "Solo" }, frontmatter.Aliases);
    }

    [Fact]
    public void Read_SurvivesAnchorExpansionWithinItsBudget()
    {
        // Each level references the previous one nine times; expanded naively this is 9^6 nodes.
        var yaml = "a: &a [x, x, x, x, x, x, x, x, x]\n" +
            "b: &b [*a, *a, *a, *a, *a, *a, *a, *a, *a]\n" +
            "c: &c [*b, *b, *b, *b, *b, *b, *b, *b, *b]\n" +
            "d: &d [*c, *c, *c, *c, *c, *c, *c, *c, *c]\n" +
            "e: &e [*d, *d, *d, *d, *d, *d, *d, *d, *d]\n" +
            "f: [*e, *e, *e, *e, *e, *e, *e, *e, *e]\n";

        var frontmatter = FrontmatterReader.Read(yaml);

        Assert.NotNull(frontmatter.Json);
        Assert.True(frontmatter.Json!.Length < 100_000);
    }

    [Fact]
    public void Read_IgnoresNonMappingDocuments()
    {
        Assert.Equal(Frontmatter.Empty, FrontmatterReader.Read("- just\n- a list"));
    }
}
