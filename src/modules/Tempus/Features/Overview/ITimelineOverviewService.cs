namespace Sentium.Tempus.Features.Overview;

/// <summary>
/// Every <c>clientToday</c> is the caller's local date, used when it is within a day of today in the user's time
/// zone; otherwise that zone's date is used.
/// </summary>
internal interface ITimelineOverviewService
{
    Task<YearOverviewDto> GetYearAsync(int year, DateOnly? clientToday, CancellationToken ct = default);

    Task<MonthDetailDto> GetMonthAsync(int year, int month, DateOnly? clientToday = null, CancellationToken ct = default);

    /// <param name="days">Length of the coming-days window, today included.</param>
    Task<UpcomingDto> GetUpcomingAsync(DateOnly? clientToday, int days, CancellationToken ct = default);
}
