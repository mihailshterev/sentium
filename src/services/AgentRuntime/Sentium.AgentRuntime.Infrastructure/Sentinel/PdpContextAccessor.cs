namespace Sentium.AgentRuntime.Infrastructure.Sentinel;

/// <summary>
/// Carries the original user prompt and correlation ID through the async execution context
/// so that the Sentinel PDP tool guard can include them in every policy evaluation.
/// Set this at the HTTP request entry-point before creating an agent.
/// </summary>
public interface IPdpContextAccessor
{
    string OriginalUserPrompt { get; set; }
    string CorrelationId { get; set; }
}

/// <summary>
/// AsyncLocal-based implementation — safe across async continuations within the same logical call context.
/// </summary>
public sealed class PdpContextAccessor : IPdpContextAccessor
{
    private static readonly AsyncLocal<string> _prompt = new();
    private static readonly AsyncLocal<string> _correlationId = new();

    public string OriginalUserPrompt
    {
        get => _prompt.Value ?? string.Empty;
        set => _prompt.Value = value ?? string.Empty;
    }

    public string CorrelationId
    {
        get => _correlationId.Value ?? string.Empty;
        set => _correlationId.Value = value ?? string.Empty;
    }
}
