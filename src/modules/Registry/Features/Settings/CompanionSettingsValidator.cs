using FluentValidation;

namespace Sentium.Registry.Features.Settings;

internal sealed class CompanionSettingsValidator : AbstractValidator<CompanionSettings>
{
    public CompanionSettingsValidator()
    {
        RuleFor(x => x.SpeakingRate)
            .InclusiveBetween(CompanionSettings.MinSpeakingRate, CompanionSettings.MaxSpeakingRate)
            .WithMessage($"SpeakingRate must be between {CompanionSettings.MinSpeakingRate} and {CompanionSettings.MaxSpeakingRate}.");

        RuleFor(x => x.Voice)
            .MaximumLength(CompanionSettings.MaxVoiceIdLength)
            .Matches("^[A-Za-z0-9_-]+$")
            .When(x => x.Voice is not null)
            .WithMessage("Voice must be a voice id, or null for the default Voice.");
    }
}
