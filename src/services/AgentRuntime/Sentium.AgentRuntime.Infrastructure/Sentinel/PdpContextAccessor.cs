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

    /// <summary>
    /// Id of the user the agent is acting on behalf of. Set at the HTTP entry-point so tools that
    /// run inside the agent loop (e.g. knowledge-base search) can scope data to the current user,
    /// even across DI scopes / approval continuations where the HTTP context is no longer available.
    /// </summary>
    Guid? UserId { get; set; }

    /// <summary>
    /// Name of the agent currently executing. Set when the agent is built so that shared tools
    /// (e.g. capture_agent_learning) can attribute their output to the real agent rather than a
    /// generic placeholder. Empty when no agent is active.
    /// </summary>
    string AgentName { get; set; }
}

/// <summary>
/// AsyncLocal-based implementation - safe across async continuations within the same logical call context.
/// </summary>
public sealed class PdpContextAccessor : IPdpContextAccessor
{
    private static readonly AsyncLocal<string> _prompt = new();
    private static readonly AsyncLocal<string> _correlationId = new();
    private static readonly AsyncLocal<Guid?> _userId = new();
    private static readonly AsyncLocal<string> _agentName = new();

    public string OriginalUserPrompt
    {
        get => _prompt.Value ?? string.Empty;
        set => _prompt.Value = value ?? string.Empty;
    }

    public string AgentName
    {
        get => _agentName.Value ?? string.Empty;
        set => _agentName.Value = value ?? string.Empty;
    }

    public string CorrelationId
    {
        get => _correlationId.Value ?? string.Empty;
        set => _correlationId.Value = value ?? string.Empty;
    }

    public Guid? UserId
    {
        get => _userId.Value;
        set => _userId.Value = value;
    }
}
