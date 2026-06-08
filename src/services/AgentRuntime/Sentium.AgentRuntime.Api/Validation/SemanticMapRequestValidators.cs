using FluentValidation;
using Sentium.AgentRuntime.Core.Dtos;

namespace Sentium.AgentRuntime.Api.Validation;

public sealed class KnowledgeMapSearchRequestValidator : AbstractValidator<KnowledgeMapSearchRequest>
{
    public KnowledgeMapSearchRequestValidator()
    {
        RuleFor(x => x.Query).NotEmpty();
    }
}
