using FluentValidation;
using Sentium.Memoria.Common;

namespace Sentium.Memoria.Features.Sync;

internal sealed class UploadNotesRequestValidator : AbstractValidator<UploadNotesRequest>
{
    public UploadNotesRequestValidator()
    {
        RuleFor(x => x.Notes)
            .NotNull()
            .Must(n => n.Count is > 0 and <= MemoriaLimits.NotesPerBatchMax)
            .WithMessage($"Send between 1 and {MemoriaLimits.NotesPerBatchMax} notes per request.");

        RuleForEach(x => x.Notes).ChildRules(note =>
        {
            note.RuleFor(n => n.Path).MustBeNotePath();
            note.RuleFor(n => n.Hash).MustBeHash();
            note.RuleFor(n => n.Content)
                .NotNull()
                .MaximumLength(MemoriaLimits.NoteContentMax)
                .WithMessage($"A note may hold at most {MemoriaLimits.NoteContentMax} characters.")
                .Must(c => c is null || !c.Contains('\0', StringComparison.Ordinal))
                .WithMessage("A note must be text; it contains a NUL character.");
        });
    }
}
