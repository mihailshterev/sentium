using FluentValidation;

namespace Sentium.Registry.Features.Settings;

internal sealed class HarnessSettingsValidator : AbstractValidator<HarnessSettings>
{
    public HarnessSettingsValidator()
    {
        RuleFor(x => x.UserHarnessPrompt)
            .MaximumLength(16_000)
            .WithMessage("UserHarnessPrompt may not exceed 16 000 characters.");
    }
}
