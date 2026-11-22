using Sentium.Sentinel.Contracts;

namespace Sentium.Sentinel.Features.Audit;

internal static class AuditStatsCalculator
{
    /// <summary>
    /// How many of the newest verdicts the alignment score averages.
    /// </summary>
    private const int AlignmentSampleSize = 20;

    /// <param name="outcomes">Newest first.</param>
    public static AuditStatsDto Calculate(IReadOnlyList<AuditOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(outcomes);

        var alignment = outcomes
            .Select(o => AlignmentScore(o.AlignmentVerdict))
            .OfType<double>()
            .Take(AlignmentSampleSize)
            .ToList();

        return new AuditStatsDto
        {
            Total = outcomes.Count,
            Allowed = outcomes.Count(o => o.Allowed),
            Denied = outcomes.Count(o => !o.Allowed),
            Alerts = outcomes.Count(o => o.Effect == PolicyEffect.DenyWithAlert),
            LowRisk = outcomes.Count(o => o.Risk == PolicyRiskLevel.Low),
            MediumRisk = outcomes.Count(o => o.Risk == PolicyRiskLevel.Medium),
            HighRisk = outcomes.Count(o => o.Risk == PolicyRiskLevel.High),
            CriticalRisk = outcomes.Count(o => o.Risk == PolicyRiskLevel.Critical),
            LatestAlignmentScore = alignment.Count > 0 ? alignment.Average() : null
        };
    }

    private static double? AlignmentScore(string? verdict) => verdict switch
    {
        "Aligned" => 1.0,
        "Inconclusive" => 0.5,
        "Misaligned" => 0.0,
        _ => null
    };
}
