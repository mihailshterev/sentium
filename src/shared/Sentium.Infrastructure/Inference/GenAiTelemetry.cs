namespace Sentium.Infrastructure.Inference;

/// <summary>
/// Source names for <c>UseOpenTelemetry</c> on chat clients and embedding generators. Microsoft.Extensions.AI
/// names both its ActivitySource and its Meter after the source name and puts no caller on its metrics, so each
/// pipeline gets its own name; Vigil reads the caller back from the meter name to break token usage down by caller.
/// </summary>
public static class GenAiTelemetry
{
    /// <summary>
    /// Prefix of every Sentium GenAI source. Service defaults export everything below it.
    /// </summary>
    public const string SourcePrefix = "Sentium.GenAI";

    /// <summary>
    /// The source Microsoft.Extensions.AI uses when a pipeline names none.
    /// </summary>
    public const string DefaultSourceName = "Experimental.Microsoft.Extensions.AI";

    public const string AgentsCaller = "Agents";
    public const string DirectCaller = "Direct";
    public const string IntentCheckCaller = "IntentCheck";
    public const string EmbeddingsCaller = "Embeddings";

    public static string SourceName(string caller)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caller);

        return $"{SourcePrefix}.{caller}";
    }
}
