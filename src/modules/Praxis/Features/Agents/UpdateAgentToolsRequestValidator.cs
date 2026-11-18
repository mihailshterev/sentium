using FluentValidation;
using Sentium.Praxis.Contracts;

namespace Sentium.Praxis.Features.Agents;

internal sealed class UpdateAgentToolsRequestValidator : AbstractValidator<UpdateAgentToolsRequest>
{
    public UpdateAgentToolsRequestValidator(IToolCatalog catalog)
    {
        RuleFor(x => x.Tools).MustBeKnownTools(catalog);
    }
}
