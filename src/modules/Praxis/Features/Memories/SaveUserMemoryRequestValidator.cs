using FluentValidation;

namespace Sentium.Praxis.Features.Memories;

internal sealed class SaveUserMemoryRequestValidator : AbstractValidator<SaveUserMemoryRequest>
{
    public SaveUserMemoryRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty()
            .MaximumLength(UserMemoryService.MaxContentLength);
    }
}
