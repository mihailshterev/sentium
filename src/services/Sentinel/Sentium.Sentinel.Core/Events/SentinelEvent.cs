namespace Sentium.Sentinel.Core.Events;

public sealed record SentinelEvent(
    string Source,
    string Type,
    string Action,
    DateTime Timestamp,
    IReadOnlyDictionary<string, string> Metadata
);
