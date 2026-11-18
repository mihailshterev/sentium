using FluentValidation;
using Sentium.Memoria.Common;

namespace Sentium.Memoria.Features.Sync;

internal sealed class VaultManifestRequestValidator : AbstractValidator<VaultManifestRequest>
{
    public VaultManifestRequestValidator()
    {
        RuleFor(x => x.Notes)
            .NotNull()
            .Must(n => n.Count <= MemoriaLimits.NotesPerVaultMax)
            .WithMessage($"A vault may hold at most {MemoriaLimits.NotesPerVaultMax} notes.");

        RuleForEach(x => x.Notes).ChildRules(entry =>
        {
            entry.RuleFor(e => e.Path).MustBeNotePath();
            entry.RuleFor(e => e.Hash).MustBeHash();
        });
    }
}
