using FluentValidation;

namespace Sentium.Codex.Features.SemanticMap;

internal sealed class KnowledgeMapSearchRequestValidator : AbstractValidator<KnowledgeMapSearchRequest>
{
    public KnowledgeMapSearchRequestValidator()
    {
        RuleFor(x => x.Query).NotEmpty();
    }
}
