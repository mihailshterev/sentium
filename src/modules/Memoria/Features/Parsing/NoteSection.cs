namespace Sentium.Memoria.Features.Parsing;

/// <summary>
/// The text under one heading (or before the first heading, with an empty <see cref="HeadingPath"/>), with wikilinks
/// already reduced to their display text.
/// </summary>
internal sealed record NoteSection(IReadOnlyList<string> HeadingPath, string Text);
