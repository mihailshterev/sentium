using FluentValidation;

namespace Sentium.Praxis.Features.Conversations;

public sealed class CreateConversationRequestValidator : AbstractValidator<CreateConversationRequest>
{
    public CreateConversationRequestValidator()
    {
        RuleFor(x => x.Title)
            .Must(title => title is null || title.Trim().Length <= ConversationTitles.MaxLength)
            .WithMessage($"Title must be at most {ConversationTitles.MaxLength} characters.");

        RuleFor(x => x.Model)
            .NotEmpty();

        RuleFor(x => x.AgentName)
            .MaximumLength(255);
    }
}
