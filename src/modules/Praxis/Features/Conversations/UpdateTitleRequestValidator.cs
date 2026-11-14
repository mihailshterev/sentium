using FluentValidation;

namespace Sentium.Praxis.Features.Conversations;

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
