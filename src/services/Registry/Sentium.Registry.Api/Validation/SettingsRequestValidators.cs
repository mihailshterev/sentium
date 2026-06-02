using FluentValidation;
using Sentium.Registry.Core.Settings;

namespace Sentium.Registry.Api.Validation;

public sealed class UpdateSettingsRequestValidator : AbstractValidator<UpdateSettingsRequest>
{
    public UpdateSettingsRequestValidator()
    {
        RuleFor(x => x.Harness.UserHarnessPrompt)
            .MaximumLength(16_000)
            .WithMessage("Harness.UserHarnessPrompt may not exceed 16 000 characters.");
    }
}
