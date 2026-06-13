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
