using FluentValidation;
using Sentium.Tempus.Dtos;
using Sentium.Tempus.Features;

namespace Sentium.Tempus.Validation;

internal static class TempusRules
{
    public static readonly string MonthRangeMessage = $"The month must be between {TempusLimits.MinYear}-01 and {TempusLimits.MaxYear}-12.";

    public static bool DayInMonth(int year, int month, DateOnly? date)
        => date is not { } day || TimelineCalendar.IsInMonth(day, year, month);
}

public sealed class SaveTimelineTaskRequestValidator : AbstractValidator<SaveTimelineTaskRequest>
{
    public SaveTimelineTaskRequestValidator()
    {
        RuleFor(x => x.Title)
            .Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("A task needs a title.")
            .MaximumLength(TempusLimits.TitleMax);

        RuleFor(x => x.Description).MaximumLength(TempusLimits.DescriptionMax);
        RuleFor(x => x.Year).InclusiveBetween(TempusLimits.MinYear, TempusLimits.MaxYear).WithMessage(TempusRules.MonthRangeMessage);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
        RuleFor(x => x.Date)
            .Must((request, date) => TempusRules.DayInMonth(request.Year, request.Month, date))
            .WithMessage("The day must be in the task's month.");
        RuleFor(x => x.DueDate)
            .Must(d => d is not { } due || due.Year is >= TempusLimits.MinYear and <= TempusLimits.MaxYear)
            .WithMessage($"The due date must be between {TempusLimits.MinYear} and {TempusLimits.MaxYear}.");
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.AssignedAgent).MaximumLength(TempusLimits.AgentNameMax);

        RuleFor(x => x.Tags)
            .Must(t => t is null || t.Count <= TempusLimits.MaxTagsPerTask)
            .WithMessage($"A task can have at most {TempusLimits.MaxTagsPerTask} tags.");

        RuleForEach(x => x.Tags)
            .Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Tags cannot be empty.")
            .Must(t => t is null || t.Trim().Length <= TempusLimits.TagMax).WithMessage($"Tags can be at most {TempusLimits.TagMax} characters.")
            .Must(t => t is null || !t.Contains(',', StringComparison.Ordinal)).WithMessage("Tags cannot contain commas.");
    }
}

public sealed class ScheduleTimelineTaskRequestValidator : AbstractValidator<ScheduleTimelineTaskRequest>
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

public sealed class SetTimelineTaskStatusRequestValidator : AbstractValidator<SetTimelineTaskStatusRequest>
{
    public SetTimelineTaskStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class CreateTimelineNoteRequestValidator : AbstractValidator<CreateTimelineNoteRequest>
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
