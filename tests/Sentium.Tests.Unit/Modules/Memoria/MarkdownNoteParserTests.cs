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

        Assert.Equal(new string[] { "project", "area/health" }, parsed.Tags);
        Assert.Equal(new string[] { "Sentium", "The Brain" }, parsed.Aliases);
        Assert.Contains("\"status\":\"active\"", parsed.FrontmatterJson, StringComparison.Ordinal);
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

        Assert.Equal(new string[] { "sentium/memoria" }, parsed.Tags);
    }

    [Fact]
    public void Parse_ReadsEveryWikilinkForm()
    {
        const string note = "See [[Note A]], [[Folder/Note B#Intro|the intro]], ![[Diagram]], [[Note C.md]] and [[#Local]].";

        var parsed = MarkdownNoteParser.Parse("x.md", note);

        Assert.Equal(new (string TargetKey, string? Heading, bool IsEmbed)[] { ("note a", null, false), ("folder/note b", "Intro", false), ("diagram", null, true), ("note c", null, false) }, parsed.Links.Select(l => (l.TargetKey, l.Heading, l.IsEmbed)));
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

        Assert.Equal(new string[] { "kept" }, parsed.Links.Select(l => l.TargetKey));
    }

    [Fact]
    public void Parse_ResolvesRelativeMarkdownLinksAgainstTheNotesFolder()
    {
        const string note = "[up](../Other%20Note.md) [same](./Sibling.md#Part) [web](https://example.com/a.md) [img](pic.png)";

        var parsed = MarkdownNoteParser.Parse("Areas/Health/Today.md", note);

        Assert.Equal(new (string TargetKey, string? Heading)[] { ("areas/other note", null), ("areas/health/sibling", "Part") }, parsed.Links.Select(l => (l.TargetKey, l.Heading)));
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

        Assert.Equal(new (string, string Text)[] { (string.Empty, "Intro text."), ("Goals", "Ship it."), ("Goals > Q1", "Memoria with the vault."), ("Notes", "Done.") }, parsed.Sections.Select(s => (string.Join(" > ", s.HeadingPath), s.Text)));
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

        Assert.Equal(new string[] { "kept" }, parsed.Tags);
        Assert.Null(parsed.FrontmatterJson);
    }

    [Fact]
    public void SimplifyLinks_ShowsAliasOrTitle()
    {
        Assert.Equal("Shown and Target > Part and Plain", MarkdownNoteParser.SimplifyLinks("[[a/b/Target|Shown]] and [[a/b/Target#Part]] and [[Plain]]"));
    }
}
