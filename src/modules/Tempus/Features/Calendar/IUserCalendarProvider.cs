using Sentium.Tempus.Common;

namespace Sentium.Tempus.Features.Calendar;

internal interface IUserCalendarProvider
{
    /// <summary>
    /// The user's own time zone and day-start where set, else the household's. Never throws: an unknown zone or a
    /// failed read falls back to UTC.
    /// </summary>
    Task<UserCalendar> GetAsync(Guid userId, CancellationToken ct = default);
}
