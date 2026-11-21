namespace Sentium.Vigil.Features.Inference;

/// <summary>
/// One read of a model's llama-server <c>/metrics</c>: running totals since the model's server process started,
/// plus the current queue.
/// </summary>
internal sealed record LlamaServerSnapshot(
    double PromptTokensTotal,
    double PromptSecondsTotal,
    double GeneratedTokensTotal,
    double GenerationSecondsTotal,
    int RequestsProcessing,
    int RequestsDeferred)
{
    private const string PromptTokens = "llamacpp:prompt_tokens_total";
    private const string PromptSeconds = "llamacpp:prompt_seconds_total";
    private const string GeneratedTokens = "llamacpp:tokens_predicted_total";
    private const string GenerationSeconds = "llamacpp:tokens_predicted_seconds_total";
    private const string Processing = "llamacpp:requests_processing";
    private const string Deferred = "llamacpp:requests_deferred";

    /// <returns><see langword="null"/> when the exposition lacks any of the counters, e.g. a server without <c>--metrics</c>.</returns>
    public static LlamaServerSnapshot? FromSamples(IReadOnlyDictionary<string, double> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (!samples.TryGetValue(PromptTokens, out var promptTokens)
            || !samples.TryGetValue(PromptSeconds, out var promptSeconds)
            || !samples.TryGetValue(GeneratedTokens, out var generatedTokens)
            || !samples.TryGetValue(GenerationSeconds, out var generationSeconds))
        {
            return null;
        }

        return new LlamaServerSnapshot(
            promptTokens,
            promptSeconds,
            generatedTokens,
            generationSeconds,
            ToCount(samples.GetValueOrDefault(Processing)),
            ToCount(samples.GetValueOrDefault(Deferred)));
    }

    private static int ToCount(double value)
    {
        return value <= 0 ? 0 : (int)Math.Min(value, int.MaxValue);
    }
}
