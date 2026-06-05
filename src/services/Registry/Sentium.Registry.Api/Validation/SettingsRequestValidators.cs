using FluentValidation;
using Sentium.Registry.Core.Settings;

namespace Sentium.Registry.Api.Validation;

public sealed class HarnessSettingsValidator : AbstractValidator<HarnessSettings>
{
    public HarnessSettingsValidator()
    {
        RuleFor(x => x.UserHarnessPrompt)
            .MaximumLength(16_000)
            .WithMessage("UserHarnessPrompt may not exceed 16 000 characters.");
    }
}

public sealed class OllamaSettingsValidator : AbstractValidator<OllamaSettings>
{
    public OllamaSettingsValidator()
    {
        RuleFor(x => x.DefaultModel)
            .NotEmpty()
            .MaximumLength(256)
            .WithMessage("DefaultModel must be a non-empty model identifier.");

        RuleFor(x => x.AgentTemperature)
            .InclusiveBetween(0.0f, 1.0f)
            .WithMessage("AgentTemperature must be between 0.0 and 1.0.");

        RuleFor(x => x.AgentContextWindow)
            .InclusiveBetween(512, 131_072)
            .WithMessage("AgentContextWindow must be between 512 and 131072.");
    }
}

public sealed class PdpSettingsValidator : AbstractValidator<PdpSettings>
{
    public PdpSettingsValidator()
    {
        RuleFor(x => x.AutonomyLevel)
            .InclusiveBetween(1, 10)
            .WithMessage("AutonomyLevel must be between 1 and 10.");

        RuleFor(x => x.RateLimitMaxRequests)
            .GreaterThanOrEqualTo(1)
            .WithMessage("RateLimitMaxRequests must be at least 1.");

        RuleFor(x => x.RateLimitWindowSeconds)
            .GreaterThanOrEqualTo(1)
            .WithMessage("RateLimitWindowSeconds must be at least 1.");

        RuleFor(x => x.IntentCheckModel)
            .MaximumLength(256)
            .WithMessage("IntentCheckModel may not exceed 256 characters.");
    }
}
