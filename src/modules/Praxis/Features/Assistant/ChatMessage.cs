namespace Sentium.Praxis.Features.Assistant;

internal sealed record ChatMessage(
    string Role,
    string Content,
    IReadOnlyList<string>? Images = null);
