namespace Sentium.Sentinel.Core.Dtos;

public sealed record AuditStatsDto
{
    public int Total { get; init; }
    public int Allowed { get; init; }
    public int Denied { get; init; }
    public int Alerts { get; init; }
    public int LowRisk { get; init; }
    public int MediumRisk { get; init; }
    public int HighRisk { get; init; }
    public int CriticalRisk { get; init; }
    public double? LatestAlignmentScore { get; init; }
}
