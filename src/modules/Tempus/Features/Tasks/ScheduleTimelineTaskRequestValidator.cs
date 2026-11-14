using FluentValidation;
using Sentium.Tempus.Common;

namespace Sentium.Tempus.Features.Tasks;

internal sealed class ScheduleTimelineTaskRequestValidator : AbstractValidator<ScheduleTimelineTaskRequest>
{
    public ScheduleTimelineTaskRequestValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(TempusLimits.MinYear, TempusLimits.MaxYear).WithMessage(TempusRules.MonthRangeMessage);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
        RuleFor(x => x.Date)
            .Must((request, date) => TempusRules.DayInMonth(request.Year, request.Month, date))
            .WithMessage("The day must be in the chosen month.");
    }
}
