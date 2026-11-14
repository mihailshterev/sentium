using FluentValidation;
using Sentium.Tempus.Common;

namespace Sentium.Tempus.Features.Notes;

internal sealed class CreateTimelineNoteRequestValidator : AbstractValidator<CreateTimelineNoteRequest>
{
    public CreateTimelineNoteRequestValidator()
    {
        RuleFor(x => x.Content)
            .Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("A note cannot be empty.")
            .MaximumLength(TempusLimits.NoteMax);
        RuleFor(x => x.Year).InclusiveBetween(TempusLimits.MinYear, TempusLimits.MaxYear).WithMessage(TempusRules.MonthRangeMessage);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
        RuleFor(x => x.Date)
            .Must((request, date) => TempusRules.DayInMonth(request.Year, request.Month, date))
            .WithMessage("The day must be in the note's month.");
        RuleFor(x => x.TaskId).Must(id => id != Guid.Empty).When(x => x.TaskId.HasValue).WithMessage("Task id is invalid.");
    }
}
