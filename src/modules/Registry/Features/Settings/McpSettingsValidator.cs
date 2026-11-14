using FluentValidation;

namespace Sentium.Registry.Features.Settings;

internal sealed class McpSettingsValidator : AbstractValidator<McpSettings>
{
    public McpSettingsValidator()
    {
        RuleFor(x => x.ConnectTimeoutSeconds)
            .InclusiveBetween(1, 300)
            .WithMessage("ConnectTimeoutSeconds must be between 1 and 300.");

        RuleFor(x => x.CallTimeoutSeconds)
            .InclusiveBetween(1, 600)
            .WithMessage("CallTimeoutSeconds must be between 1 and 600.");

        RuleFor(x => x.IdleTimeoutMinutes)
            .InclusiveBetween(1, 1_440)
            .WithMessage("IdleTimeoutMinutes must be between 1 and 1440.");

        RuleFor(x => x.MaxResultChars)
            .InclusiveBetween(1_000, 200_000)
            .WithMessage("MaxResultChars must be between 1000 and 200000.");
    }
}
