using FluentValidation;

namespace Sentium.Praxis.Features.Orchestration;

internal sealed class RunDynamicWorkflowRequestValidator : AbstractValidator<RunDynamicWorkflowRequest>
{
    private const int MaxActivityLength = 16_000;

    public RunDynamicWorkflowRequestValidator()
    {
        RuleFor(x => x.Activity).NotEmpty().MaximumLength(MaxActivityLength);
    }
}
