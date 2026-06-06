namespace Sentium.Sentinel.Core.Dtos;

public sealed record PdpSettingsDto
{
    public bool LockdownMode { get; init; }
    public int AutonomyLevel { get; init; }
    public bool SemanticIntentCheckEnabled { get; init; }
    public string IntentCheckModel { get; init; } = string.Empty;
    public int RateLimitMaxRequests { get; init; }
    public int RateLimitWindowSeconds { get; init; }
}

public sealed record UpdatePdpSettingsRequest
{
    public bool? LockdownMode { get; init; }
    public int? AutonomyLevel { get; init; }
    public bool? SemanticIntentCheckEnabled { get; init; }
    public string? IntentCheckModel { get; init; }
    public int? RateLimitMaxRequests { get; init; }
    public int? RateLimitWindowSeconds { get; init; }
}
