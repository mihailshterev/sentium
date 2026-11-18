namespace Sentium.Probatio.Features.Suites;

internal sealed record EvalSuiteSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    int CurrentVersion,
    int CaseCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
