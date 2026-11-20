using Sentium.Memoria.Features.Parsing;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Memoria;

public sealed class MarkdownNoteChunkerTests
{
    [Fact]
    public void Chunk_PrefixesEachSectionWithTitleAndHeadingPath()
    {
        var chunks = MarkdownNoteChunker.Chunk("Plan", ["work"],
        [
            new NoteSection([], "Intro."),
            new NoteSection(["Goals", "Q1"], "Ship it.")
        ]);

        Assert.Equal(new string[] { "Plan\n\nIntro.\n\nTags: work", "Plan > Goals > Q1\n\nShip it." }, chunks);
    }

    [Fact]
    public void Chunk_SplitsLongSectionsWithinTheLimit()
    {
        var paragraph = string.Join(' ', Enumerable.Repeat("word", 300));
        var text = string.Join("\n\n", Enumerable.Repeat(paragraph, 4));

        var chunks = MarkdownNoteChunker.Chunk("Long", [], [new NoteSection(["H"], text)]);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.StartsWith("Long > H\n\n", c, StringComparison.Ordinal));
        Assert.All(chunks, c => Assert.True(c.Length <= MarkdownNoteChunker.MaxChunkChars + "Long > H\n\n".Length));
    }

    [Fact]
    public void Chunk_KeepsATitleOnlyNoteFindable()
    {
        Assert.Equal(new string[] { "Empty > Only heading" }, MarkdownNoteChunker.Chunk("Empty", [], [new NoteSection(["Only heading"], string.Empty)]));
    }
}
