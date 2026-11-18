namespace Sentium.Memoria.Features.Parsing;

internal sealed record ParsedNote(
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Aliases,
    string? FrontmatterJson,
    IReadOnlyList<ParsedLink> Links,
    IReadOnlyList<NoteSection> Sections,
    int WordCount);
