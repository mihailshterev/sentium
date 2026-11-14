using FluentValidation;

namespace Sentium.Registry.Features.Settings;

internal sealed class UserCalendarSettingsValidator : AbstractValidator<UserCalendarSettings>
{
    public UserCalendarSettingsValidator()
    {
        RuleFor(x => x.TimeZone)
            .Must(z => z is null || TimeZones.IsKnown(z))
            .WithMessage("TimeZone must be a known IANA time zone id, for example Europe/Sofia, or null for the household's.");
    }
}
