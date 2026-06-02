using FluentValidation;
using Sentium.AgentRuntime.Core.Dtos;

namespace Sentium.AgentRuntime.Api.Validation;

public sealed class CreateAgentRequestValidator : AbstractValidator<CreateAgentRequest>
{
    public CreateAgentRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(2000);

        RuleFor(x => x.Model)
            .MaximumLength(100);
    }
}

public sealed class UpdateAgentRequestValidator : AbstractValidator<UpdateAgentRequest>
{
    public UpdateAgentRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(2000);

        RuleFor(x => x.Model)
            .MaximumLength(100);
    }
}
