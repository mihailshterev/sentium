namespace Sentinel.Core.Decisions;

public sealed record SentinelDecision(
    bool Allowed,
    string Reason
);
