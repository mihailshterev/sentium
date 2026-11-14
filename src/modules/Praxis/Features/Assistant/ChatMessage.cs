namespace Sentium.Praxis.Features.Assistant;

public sealed record ChatMessage(
    string Role,
    string Content,
    IReadOnlyList<string>? Images = null);
