using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sentium.Tempus.Features.Overview;

/// <summary>
/// The signed-in user's timeline: month summaries for the year path, the full month for the calendar, and what
/// needs attention next.
/// </summary>
[ApiController]
[Authorize]
[Route(TempusRoutes.Prefix)]
public sealed class TimelineController(ITimelineOverviewService overviewService) : ControllerBase
{
    /// <summary>
    /// Summarises every month of a year (task counts, overdue tasks, notes and pending agent proposals) and every
    /// day with tasks planned on it.
    /// </summary>
    /// <param name="year">The year (2000-2100).</param>
    /// <param name="today">The caller's local date, used for the overdue counts when within a day of UTC.</param>
    [HttpGet("years/{year:int:range(2000,2100)}")]
    [ProducesResponseType(typeof(YearOverviewDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetYear(int year, [FromQuery] DateOnly? today, CancellationToken ct)
        => Ok(await overviewService.GetYearAsync(year, today, ct));

    /// <summary>
    /// The month's tasks, notes and pending agent proposals.
    /// </summary>
    [HttpGet("years/{year:int:range(2000,2100)}/months/{month:int:range(1,12)}")]
    [ProducesResponseType(typeof(MonthDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMonth(int year, int month, CancellationToken ct)
        => Ok(await overviewService.GetMonthAsync(year, month, ct));

    /// <summary>
    /// Overdue tasks, the open tasks of the coming days and the number of pending agent suggestions.
    /// </summary>
    /// <param name="today">The caller's local date, used when within a day of UTC.</param>
    /// <param name="days">Length of the coming-days window, today included (1-42, default 7).</param>
    [HttpGet("upcoming")]
    [ProducesResponseType(typeof(UpcomingDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUpcoming([FromQuery] DateOnly? today, [FromQuery] int days = 7, CancellationToken ct = default)
        => Ok(await overviewService.GetUpcomingAsync(today, days, ct));
}
