namespace Sentium.Praxis.Features.Sentinel;

/// <summary>
/// Ambient context of the current agent run - acting user, correlation id, and original prompt - carried through the
/// async execution context so tools and the Sentinel guard can scope data and policy decisions to the run.
/// <para>
/// AsyncLocal semantics: values must be set by the run's entry point, in the method that awaits the run (the
/// assistant controller, the workflow processor). A value assigned inside a callee's <c>async</c> method never flows
/// back to its caller. The executing agent's name comes from Agent Framework's own <c>AIAgent.CurrentRunContext</c>.
/// </para>
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
}

/// <summary>
/// AsyncLocal-based implementation - safe across async continuations within the same logical call context.
/// </summary>
public sealed class PdpContextAccessor : IPdpContextAccessor
{
    private static readonly AsyncLocal<string> _prompt = new();
    private static readonly AsyncLocal<string> _correlationId = new();
    private static readonly AsyncLocal<Guid?> _userId = new();

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

    public Guid? UserId
    {
        get => _userId.Value;
        set => _userId.Value = value;
    }
}
