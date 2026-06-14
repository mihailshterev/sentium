namespace Sentium.AgentRuntime.Infrastructure.Registry;

public static class AgentRuntimeSettingsCacheKeys
{
    public const string SnapshotTag = "settings:agent-snapshot";

    public static string Snapshot(Guid? userId) => $"settings:agent-snapshot:{userId?.ToString() ?? "global"}";
}
