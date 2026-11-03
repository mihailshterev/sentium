using FluentValidation;
using Sentium.Codex.Dtos;

namespace Sentium.Codex.Validation;

public sealed class KnowledgeMapSearchRequestValidator : AbstractValidator<KnowledgeMapSearchRequest>
{
    public KnowledgeMapSearchRequestValidator()
    {
        RuleFor(x => x.Query).NotEmpty();
    }
}
