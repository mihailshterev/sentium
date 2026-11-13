using FluentValidation;
using Sentium.Praxis.Features.Agents;

namespace Sentium.Praxis.Features.Assistant;

public sealed class ChatRequestValidator : AbstractValidator<ChatRequest>
{
    public ChatRequestValidator()
    {
        RuleFor(x => x.Messages)
            .NotEmpty()
            .WithMessage("At least one message is required.");

        RuleForEach(x => x.Messages).ChildRules(message =>
        {
            message.RuleFor(m => m.Role).NotEmpty();
            message.RuleFor(m => m.Content).NotNull();
        });
    }
}
