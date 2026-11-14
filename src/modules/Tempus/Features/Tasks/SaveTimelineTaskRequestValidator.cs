using FluentValidation;
using Sentium.Tempus.Common;

namespace Sentium.Tempus.Features.Tasks;

internal sealed class SaveTimelineTaskRequestValidator : AbstractValidator<SaveTimelineTaskRequest>
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
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.AssignedAgent).MaximumLength(TempusLimits.AgentNameMax);

        RuleFor(x => x.Tags)
            .Must(t => t is null || t.Count <= TempusLimits.MaxTagsPerTask)
            .WithMessage($"A task can have at most {TempusLimits.MaxTagsPerTask} tags.");

        RuleForEach(x => x.Tags)
            .Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Tags cannot be empty.")
            .Must(t => t is null || t.Trim().Length <= TempusLimits.TagMax).WithMessage($"Tags can be at most {TempusLimits.TagMax} characters.")
            .Must(t => t is null || !t.Contains(',', StringComparison.Ordinal)).WithMessage("Tags cannot contain commas.");

        RuleFor(x => x).Custom((request, context) =>
        {
            if (TaskRules.ValidateEntry(request) is { } error)
            {
                context.AddFailure(error);
            }
        });
    }
}
