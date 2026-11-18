using FluentAssertions;
using Sentium.Memoria.Features.Parsing;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Memoria;

public sealed class MarkdownNoteParserTests
{
    [Fact]
    public void Parse_ReadsFrontmatterTagsAndAliases()
    {
        const string note = """
            ---
            tags: [Project, area/health]
            aliases:
              - Sentium
              - The Brain
            status: active
            ---
            # Hello
            """;

        var parsed = MarkdownNoteParser.Parse("Projects/Sentium.md", note);

        parsed.Tags.Should().Equal("project", "area/health");
        parsed.Aliases.Should().Equal("Sentium", "The Brain");
        parsed.FrontmatterJson.Should().Contain("\"status\":\"active\"");
    }

    [Fact]
    public void Parse_FindsInlineTagsButNotNumbersHeadingsOrCode()
    {
        const string note = """
            # Heading
            Working on #sentium/memoria and #2024 today, see `#notatag`.

            ```
            #alsonotatag
            ```
            """;

        var parsed = MarkdownNoteParser.Parse("a.md", note);

        parsed.Tags.Should().Equal("sentium/memoria");
    }

    [Fact]
    public void Parse_ReadsEveryWikilinkForm()
    {
        const string note = "See [[Note A]], [[Folder/Note B#Intro|the intro]], ![[Diagram]], [[Note C.md]] and [[#Local]].";

        var parsed = MarkdownNoteParser.Parse("x.md", note);

        parsed.Links.Select(l => (l.TargetKey, l.Heading, l.IsEmbed)).Should().Equal(
            ("note a", null, false),
            ("folder/note b", "Intro", false),
            ("diagram", null, true),
            ("note c", null, false));
    }

    [Fact]
    public void Parse_IgnoresLinksInCodeAndAttachments()
    {
        const string note = """
            Real [[Kept]] link, `[[Inline code]]`, and ![[photo.png]].

            ```markdown
            [[Fenced]]
            ```

            %% [[Commented]] %%
            """;

        var parsed = MarkdownNoteParser.Parse("x.md", note);

        parsed.Links.Select(l => l.TargetKey).Should().Equal("kept");
    }

    [Fact]
    public void Parse_ResolvesRelativeMarkdownLinksAgainstTheNotesFolder()
    {
        const string note = "[up](../Other%20Note.md) [same](./Sibling.md#Part) [web](https://example.com/a.md) [img](pic.png)";

        var parsed = MarkdownNoteParser.Parse("Areas/Health/Today.md", note);

        parsed.Links.Select(l => (l.TargetKey, l.Heading)).Should().Equal(
            ("areas/other note", null),
            ("areas/health/sibling", "Part"));
    }

    [Fact]
    public void Parse_SplitsSectionsByHeadingPath()
    {
        const string note = """
            Intro text.

            # Goals
            Ship it.

            ## Q1
            Memoria with [[Obsidian|the vault]].

            # Notes
            Done.
            """;

        var parsed = MarkdownNoteParser.Parse("x.md", note);

        parsed.Sections.Select(s => (string.Join(" > ", s.HeadingPath), s.Text)).Should().Equal(
            (string.Empty, "Intro text."),
            ("Goals", "Ship it."),
            ("Goals > Q1", "Memoria with the vault."),
            ("Notes", "Done."));
    }

    [Fact]
    public void Parse_TreatsBrokenFrontmatterAsNone()
    {
        const string note = """
            ---
            tags: [unclosed
            ---
            Body #kept
            """;

        var parsed = MarkdownNoteParser.Parse("x.md", note);

        parsed.Tags.Should().Equal("kept");
        parsed.FrontmatterJson.Should().BeNull();
    }

    [Fact]
    public void SimplifyLinks_ShowsAliasOrTitle()
    {
        MarkdownNoteParser.SimplifyLinks("[[a/b/Target|Shown]] and [[a/b/Target#Part]] and [[Plain]]")
            .Should().Be("Shown and Target > Part and Plain");
    }
}
