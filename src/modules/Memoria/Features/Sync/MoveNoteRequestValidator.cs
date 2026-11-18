using FluentValidation;

namespace Sentium.Memoria.Features.Sync;

internal sealed class MoveNoteRequestValidator : AbstractValidator<MoveNoteRequest>
{
    public MoveNoteRequestValidator()
    {
        RuleFor(x => x.From).MustBeNotePath();
        RuleFor(x => x.To).MustBeNotePath();
    }
}
