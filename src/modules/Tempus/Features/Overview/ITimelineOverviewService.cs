namespace Sentium.Tempus.Features.Overview;

internal interface ITimelineOverviewService
{
    /// <param name="clientToday">The caller's local date, used for overdue counts when it is plausible.</param>
    Task<YearOverviewDto> GetYearAsync(int year, DateOnly? clientToday, CancellationToken ct = default);

    Task<MonthDetailDto> GetMonthAsync(int year, int month, CancellationToken ct = default);

    /// <param name="clientToday">The caller's local date, used when it is plausible.</param>
    /// <param name="days">Length of the coming-days window, today included.</param>
    Task<UpcomingDto> GetUpcomingAsync(DateOnly? clientToday, int days, CancellationToken ct = default);
}
