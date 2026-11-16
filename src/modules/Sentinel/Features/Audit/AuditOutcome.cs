using Sentium.Sentinel.Features.Policies;

namespace Sentium.Sentinel.Features.Audit;

/// <summary>
/// The part of an audit record the statistics need.
/// </summary>
internal sealed record AuditOutcome(bool Allowed, PolicyEffect Effect, PolicyRiskLevel Risk, string? AlignmentVerdict);
