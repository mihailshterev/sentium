namespace Sentium.Praxis.Features.Models;

/// <summary>
/// Which chat models the inference server still serves, so a conversation or setting naming a model that was removed
/// from <c>infra/llm/models.ini</c> falls back instead of failing every request.
/// </summary>
internal interface IInstalledModels
{
    /// <summary>
    /// The first of <paramref name="candidates"/> (most preferred first) the server declares. When none is declared,
    /// or the server cannot be asked, the first candidate: an unreachable server is not a reason to switch models.
    /// </summary>
    ValueTask<string> PickAsync(IReadOnlyList<string> candidates, CancellationToken ct = default);
}
