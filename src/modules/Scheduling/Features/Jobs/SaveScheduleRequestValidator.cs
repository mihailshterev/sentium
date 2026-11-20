using FluentValidation;

namespace Sentium.Scheduling.Features.Jobs;

internal sealed class SaveScheduleRequestValidator : AbstractValidator<SaveScheduleRequest>
{
    public const int NameMax = 120;
    public const int PromptMax = 8000;
    public const int MaxPreApprovedTools = 50;

    public SaveScheduleRequestValidator()
    {
        RuleFor(x => x.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Give the schedule a name.")
            .MaximumLength(NameMax);

        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.CronExpression).NotEmpty().WithMessage("Choose when the schedule runs.");

        RuleFor(x => x.AgentName)
            .Must(a => !string.IsNullOrWhiteSpace(a)).WithMessage("Choose an agent.")
            .When(x => x.Kind == ScheduleKind.Agent);

        RuleFor(x => x.WorkflowId)
            .NotEmpty().WithMessage("Choose a workflow.")
            .When(x => x.Kind == ScheduleKind.Workflow);

        RuleFor(x => x.Prompt)
            .Must(p => !string.IsNullOrWhiteSpace(p)).WithMessage("Write the prompt the agent runs.")
            .When(x => x.Kind == ScheduleKind.Agent);

        RuleFor(x => x.Prompt).MaximumLength(PromptMax);

        RuleFor(x => x.PreApprovedTools)
            .Must(t => t is null || t.Count <= MaxPreApprovedTools)
            .WithMessage($"Pre-approve at most {MaxPreApprovedTools} tools.");
    }
}
