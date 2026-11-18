using FluentValidation;
using Sentium.Praxis.Contracts;

namespace Sentium.Probatio.Features.Suites;

internal sealed class CreateEvalSuiteRequestValidator : AbstractValidator<CreateEvalSuiteRequest>
{
    public CreateEvalSuiteRequestValidator(IToolCatalog catalog)
    {
        Include(new EvalSuiteContentValidator(catalog));
    }
}
