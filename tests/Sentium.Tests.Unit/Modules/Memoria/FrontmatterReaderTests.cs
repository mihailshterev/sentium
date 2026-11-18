using FluentAssertions;
using Sentium.Memoria.Features.Parsing;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Memoria;

public sealed class FrontmatterReaderTests
{
    [Fact]
    public void Read_AcceptsCommaSeparatedStringsAndHashPrefixes()
    {
        var frontmatter = FrontmatterReader.Read("tags: \"#one, two three\"\nalias: Solo");

        frontmatter.Tags.Should().Equal("one", "two", "three");
        frontmatter.Aliases.Should().Equal("Solo");
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

        frontmatter.Json.Should().NotBeNull();
        frontmatter.Json!.Length.Should().BeLessThan(100_000);
    }

    [Fact]
    public void Read_IgnoresNonMappingDocuments()
    {
        FrontmatterReader.Read("- just\n- a list").Should().Be(Frontmatter.Empty);
    }
}
