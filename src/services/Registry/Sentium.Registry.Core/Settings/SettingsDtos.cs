namespace Sentium.Registry.Core.Settings;

public sealed record SettingsDto(
    HarnessSettingsDto Harness,
    DateTimeOffset UpdatedAt,
    string? UpdatedBy);

public sealed record HarnessSettingsDto(
    string UserHarnessPrompt,
    bool IsBuiltInHarnessEnabled);

public sealed record UpdateSettingsRequest(UpdateHarnessSettingsRequest Harness);

public sealed record UpdateHarnessSettingsRequest(
    string UserHarnessPrompt,
    bool IsBuiltInHarnessEnabled);
