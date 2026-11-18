using FluentAssertions;
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

        chunks.Should().Equal("Plan\n\nIntro.\n\nTags: work", "Plan > Goals > Q1\n\nShip it.");
    }

    [Fact]
    public void Chunk_SplitsLongSectionsWithinTheLimit()
    {
        var paragraph = string.Join(' ', Enumerable.Repeat("word", 300));
        var text = string.Join("\n\n", Enumerable.Repeat(paragraph, 4));

        var chunks = MarkdownNoteChunker.Chunk("Long", [], [new NoteSection(["H"], text)]);

        chunks.Should().HaveCountGreaterThan(1);
        chunks.Should().AllSatisfy(c => c.Should().StartWith("Long > H\n\n"));
        chunks.Should().AllSatisfy(c => c.Length.Should().BeLessThanOrEqualTo(MarkdownNoteChunker.MaxChunkChars + "Long > H\n\n".Length));
    }

    [Fact]
    public void Chunk_KeepsATitleOnlyNoteFindable()
    {
        MarkdownNoteChunker.Chunk("Empty", [], [new NoteSection(["Only heading"], string.Empty)])
            .Should().Equal("Empty > Only heading");
    }
}
