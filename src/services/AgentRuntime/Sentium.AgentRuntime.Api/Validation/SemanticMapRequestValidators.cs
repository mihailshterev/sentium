using FluentValidation;
using Sentium.AgentRuntime.Api.Controllers;

namespace Sentium.AgentRuntime.Api.Validation;

public sealed class KnowledgeMapSearchRequestValidator : AbstractValidator<KnowledgeMapSearchRequest>
{
    public KnowledgeMapSearchRequestValidator()
    {
        RuleFor(x => x.Query).NotEmpty();
    }
}
