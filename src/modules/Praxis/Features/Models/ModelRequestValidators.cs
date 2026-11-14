using FluentValidation;

namespace Sentium.Praxis.Features.Models;

public sealed class ModelActionRequestValidator : AbstractValidator<ModelActionRequest>
{
    public ModelActionRequestValidator()
    {
        RuleFor(x => x.Model)
            .NotEmpty()
            .WithMessage("Model must be a non-empty model id.")
            .MaximumLength(256)
            .WithMessage("Model may not exceed 256 characters.");
    }
}
