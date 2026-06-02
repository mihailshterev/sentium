namespace Sentium.AgentRuntime.Core.Agents;

/// <summary>
/// Runs a pre-execution pass that rewrites a raw user prompt into a clearer, more specific optimized
/// prompt, before the main agent executes.
/// </summary>
public interface IPromptEnhancementService
{
    /// <summary>
    /// Rewrites <paramref name="prompt"/> for best results. Returns the original prompt unchanged on
    /// any failure (fail-open) so enhancement can never break or block an interaction.
    /// </summary>
    Task<string> EnhanceAsync(string prompt, CancellationToken ct = default);
}
