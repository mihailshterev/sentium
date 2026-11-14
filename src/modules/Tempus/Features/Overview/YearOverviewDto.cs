namespace Sentium.Tempus.Features.Overview;

/// <param name="Today">The date overdue counts were computed for.</param>
/// <param name="Months">Always twelve entries, January first.</param>
/// <param name="Days">Only days with tasks planned on them, in date order.</param>
internal sealed record YearOverviewDto(
    int Year,
    DateOnly Today,
    IReadOnlyList<MonthSummaryDto> Months,
    IReadOnlyList<DaySummaryDto> Days);
