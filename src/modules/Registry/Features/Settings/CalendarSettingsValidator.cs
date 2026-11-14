using FluentValidation;

namespace Sentium.Registry.Features.Settings;

internal sealed class CalendarSettingsValidator : AbstractValidator<CalendarSettings>
{
    public CalendarSettingsValidator()
    {
        RuleFor(x => x.TimeZone)
            .Must(TimeZones.IsKnown)
            .WithMessage("TimeZone must be a known IANA time zone id, for example Europe/Sofia.");
    }
}
