using FluentValidation;
using Sentium.Memoria.Common;

namespace Sentium.Memoria.Features.Sync;

internal sealed class RemoveNotesRequestValidator : AbstractValidator<RemoveNotesRequest>
{
    public RemoveNotesRequestValidator()
    {
        RuleFor(x => x.Paths)
            .NotNull()
            .Must(p => p.Count is > 0 and <= MemoriaLimits.PathsPerBatchMax)
            .WithMessage($"Send between 1 and {MemoriaLimits.PathsPerBatchMax} paths per request.");

        RuleForEach(x => x.Paths).MustBeNotePath();
    }
}
