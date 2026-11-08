using FluentValidation;
using Sentium.Praxis.Dtos;

namespace Sentium.Praxis.Validation;

public sealed class UpdateTitleRequestValidator : AbstractValidator<UpdateTitleRequest>
{
    public UpdateTitleRequestValidator()
    {
        RuleFor(x => x.Title)
            .Must(title => !string.IsNullOrWhiteSpace(title))
            .WithMessage("Title must not be empty.")
            .Must(title => title is null || title.Trim().Length <= ConversationTitles.MaxLength)
            .WithMessage($"Title must be at most {ConversationTitles.MaxLength} characters.");
    }
}

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
