using FluentValidation;
using Sentium.Sentinel.Core.Dtos;
using Sentium.Sentinel.Core.Policies;

namespace Sentium.Sentinel.Api.Validation;

public sealed class PolicyEvaluationRequestValidator : AbstractValidator<PolicyEvaluationRequest>
{
    public PolicyEvaluationRequestValidator()
    {
        RuleFor(x => x.ResourceType)
            .Must(rt => Enum.TryParse<ResourceType>(rt, ignoreCase: true, out _))
            .WithMessage(x => $"Unknown resource type '{x.ResourceType}'. Valid values: {string.Join(", ", Enum.GetNames<ResourceType>())}");
    }
}

public sealed class UpdatePdpSettingsRequestValidator : AbstractValidator<UpdatePdpSettingsRequest>
{
    public UpdatePdpSettingsRequestValidator()
    {
        RuleFor(x => x.AutonomyLevel)
            .InclusiveBetween(1, 10)
            .When(x => x.AutonomyLevel.HasValue)
            .WithMessage("AutonomyLevel must be between 1 and 10.");

        RuleFor(x => x.RateLimitMaxRequests)
            .GreaterThanOrEqualTo(1)
            .When(x => x.RateLimitMaxRequests.HasValue)
            .WithMessage("RateLimitMaxRequests must be at least 1.");

        RuleFor(x => x.RateLimitWindowSeconds)
            .GreaterThanOrEqualTo(1)
            .When(x => x.RateLimitWindowSeconds.HasValue)
            .WithMessage("RateLimitWindowSeconds must be at least 1.");
    }
}
