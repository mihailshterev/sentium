using FluentValidation;

namespace Sentium.Praxis.Features.Speech;

internal sealed class SpeakRequestValidator : AbstractValidator<SpeakRequest>
{
    public SpeakRequestValidator()
    {
        RuleFor(x => x.Text)
            .NotEmpty()
            .MaximumLength(SpeechLimits.MaxTextLength);

        // The id is forwarded to the speech server, so keep it to the characters voice ids are made of.
        RuleFor(x => x.Voice)
            .MaximumLength(SpeechLimits.MaxVoiceIdLength)
            .Matches("^[A-Za-z0-9_-]+$")
            .When(x => x.Voice is not null)
            .WithMessage("Voice must be a voice id.");

        RuleFor(x => x.Speed)
            .InclusiveBetween(SpeechLimits.MinSpeed, SpeechLimits.MaxSpeed)
            .When(x => x.Speed is not null);
    }
}
