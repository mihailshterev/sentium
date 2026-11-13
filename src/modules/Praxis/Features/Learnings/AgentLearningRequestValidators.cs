using FluentValidation;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Learnings;

namespace Sentium.Praxis.Features.Learnings;

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
