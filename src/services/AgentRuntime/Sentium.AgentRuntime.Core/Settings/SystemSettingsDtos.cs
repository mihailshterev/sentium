namespace Sentium.AgentRuntime.Core.Settings;

public sealed record SystemSettingsDto(
    string UserHarnessPrompt,
    bool IsBuiltInHarnessEnabled,
    DateTimeOffset UpdatedAt,
    string? UpdatedBy);

public sealed record UpdateSystemSettingsRequest(
    string UserHarnessPrompt,
    bool IsBuiltInHarnessEnabled);
