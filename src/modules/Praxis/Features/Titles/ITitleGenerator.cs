namespace Sentium.Praxis.Features.Titles;

/// <summary>
/// Produces short display titles (conversations, workflow runs) from the text a user submitted.
/// </summary>
internal interface ITitleGenerator
{
    /// <summary>
    /// Asks the model for a concise title summarizing <paramref name="text"/>. Never throws: on any failure, cancellation or
    /// unusable output it falls back to <see cref="TitleGenerator.FromText"/>, and returns <see langword="null"/> only when
    /// the text holds nothing to title.
    /// </summary>
    Task<string?> GenerateAsync(string text, CancellationToken ct = default);
}
