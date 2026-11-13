using Sentium.Praxis.Contracts;

namespace Sentium.Praxis.Features.Sentinel;

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
