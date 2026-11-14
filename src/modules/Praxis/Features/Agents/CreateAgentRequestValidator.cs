using FluentValidation;
using Sentium.Praxis.Features.Tools;

namespace Sentium.Praxis.Features.Agents;

public sealed class CreateAgentRequestValidator : AbstractValidator<CreateAgentRequest>
{
    public CreateAgentRequestValidator(IToolCatalog catalog)
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(AgentToolRules.MaxDescriptionLength);

        RuleFor(x => x.Model)
            .MaximumLength(100);

        RuleFor(x => x.Tools).MustBeKnownTools(catalog);
    }
}
