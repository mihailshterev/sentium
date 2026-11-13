using FluentValidation;
using Sentium.Registry.Features.Settings;

namespace Sentium.Registry.Features.Settings;

public sealed class HarnessSettingsValidator : AbstractValidator<HarnessSettings>
{
    public HarnessSettingsValidator()
    {
        RuleFor(x => x.UserHarnessPrompt)
            .MaximumLength(16_000)
            .WithMessage("UserHarnessPrompt may not exceed 16 000 characters.");
    }
}

public sealed class InferenceSettingsValidator : AbstractValidator<InferenceSettings>
{
    public InferenceSettingsValidator()
    {
        RuleFor(x => x.DefaultModel)
            .NotEmpty()
            .MaximumLength(256)
            .WithMessage("DefaultModel must be a non-empty model identifier.");

        RuleFor(x => x.AgentTemperature)
            .InclusiveBetween(0.0f, 1.0f)
            .WithMessage("AgentTemperature must be between 0.0 and 1.0.");
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

public sealed class VigilSettingsValidator : AbstractValidator<VigilSettings>
{
    public VigilSettingsValidator()
    {
        RuleFor(x => x.PollIntervalSeconds)
            .InclusiveBetween(5, 3_600)
            .WithMessage("PollIntervalSeconds must be between 5 and 3600.");

        RuleFor(x => x.ProbeTimeoutSeconds)
            .InclusiveBetween(1, 60)
            .WithMessage("ProbeTimeoutSeconds must be between 1 and 60.");

        RuleFor(x => x.ProbeTimeoutSeconds)
            .LessThanOrEqualTo(x => x.PollIntervalSeconds)
            .WithMessage("ProbeTimeoutSeconds must not exceed PollIntervalSeconds.");

        RuleFor(x => x.DegradedLatencyMs)
            .InclusiveBetween(1, 60_000)
            .WithMessage("DegradedLatencyMs must be between 1 and 60000.");

        RuleFor(x => x.ConsecutiveFailuresToOpenIncident)
            .InclusiveBetween(1, 100)
            .WithMessage("ConsecutiveFailuresToOpenIncident must be between 1 and 100.");

        RuleFor(x => x.SampleHistorySize)
            .InclusiveBetween(10, 200)
            .WithMessage("SampleHistorySize must be between 10 and 200.");
    }
}

public sealed class McpSettingsValidator : AbstractValidator<McpSettings>
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
