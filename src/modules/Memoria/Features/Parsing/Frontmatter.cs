namespace Sentium.Memoria.Features.Parsing;

internal sealed record Frontmatter(IReadOnlyList<string> Tags, IReadOnlyList<string> Aliases, string? Json)
{
    public static Frontmatter Empty { get; } = new([], [], null);
}
