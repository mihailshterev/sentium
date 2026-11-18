using FluentValidation;
using Sentium.Memoria.Common;

namespace Sentium.Memoria.Features.Sync;

internal sealed class ClaimVaultRequestValidator : AbstractValidator<ClaimVaultRequest>
{
    public ClaimVaultRequestValidator()
    {
        RuleFor(x => x.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("A vault name is required.")
            .MaximumLength(MemoriaLimits.VaultNameMax);
    }
}
