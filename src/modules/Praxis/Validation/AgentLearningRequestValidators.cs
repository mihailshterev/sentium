using FluentValidation;
using Sentium.Praxis.Features.Learnings;

namespace Sentium.Praxis.Validation;

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
