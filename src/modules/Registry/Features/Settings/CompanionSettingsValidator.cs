using FluentValidation;
using Sentium.Registry.Contracts.Settings;

namespace Sentium.Registry.Features.Settings;

internal sealed class CompanionSettingsValidator : AbstractValidator<CompanionSettings>
{
    private const string VoiceIdPattern = "^[A-Za-z0-9_-]+$";

    public CompanionSettingsValidator()
    {
        RuleFor(x => x.Voice)
            .Matches(VoiceIdPattern)
            .When(x => x.Voice is not null)
            .WithMessage("Voice must be a voice id, or null for the default Voice.");
    }
}
