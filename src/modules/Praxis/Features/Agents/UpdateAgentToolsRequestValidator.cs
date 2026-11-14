using FluentValidation;
using Sentium.Praxis.Features.Tools;

namespace Sentium.Praxis.Features.Agents;

internal sealed class UpdateAgentToolsRequestValidator : AbstractValidator<UpdateAgentToolsRequest>
{
    public UpdateAgentToolsRequestValidator(IToolCatalog catalog)
    {
        RuleFor(x => x.Tools).MustBeKnownTools(catalog);
    }
}
