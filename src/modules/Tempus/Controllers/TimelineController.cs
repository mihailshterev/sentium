using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Tempus.Dtos;
using Sentium.Tempus.Features.Overview;

namespace Sentium.Tempus.Controllers;

/// <summary>
/// The signed-in user's year timeline: month summaries for the node view and the full month for the calendar.
/// </summary>
[ApiController]
[Authorize]
[Route($"{TempusRoutes.Prefix}/years")]
public sealed class TimelineController(ITimelineOverviewService overviewService) : ControllerBase
{
    /// <summary>
    /// Summarises every month of a year: task counts, overdue tasks, notes and pending agent proposals.
    /// </summary>
    /// <param name="year">The year (2000-2100).</param>
    /// <param name="today">The caller's local date, used for the overdue counts when within a day of UTC.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{year:int:range(2000,2100)}")]
    [ProducesResponseType(typeof(YearOverviewDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetYear(int year, [FromQuery] DateOnly? today, CancellationToken ct)
        => Ok(await overviewService.GetYearAsync(year, today, ct));

    /// <summary>
    /// The month's tasks, notes and pending agent proposals.
    /// </summary>
    [HttpGet("{year:int:range(2000,2100)}/months/{month:int:range(1,12)}")]
    [ProducesResponseType(typeof(MonthDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMonth(int year, int month, CancellationToken ct)
        => Ok(await overviewService.GetMonthAsync(year, month, ct));
}
