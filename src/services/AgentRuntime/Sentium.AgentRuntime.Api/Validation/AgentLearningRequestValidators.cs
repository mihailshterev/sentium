using FluentValidation;
using Sentium.AgentRuntime.Core.Learnings;

namespace Sentium.AgentRuntime.Api.Validation;

public sealed class UpdateAgentLearningRequestValidator : AbstractValidator<UpdateAgentLearningRequest>
{
    public UpdateAgentLearningRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty()
            .MaximumLength(10_000);

        RuleFor(x => x.Tags)
            .MaximumLength(1_000);
    }
}
