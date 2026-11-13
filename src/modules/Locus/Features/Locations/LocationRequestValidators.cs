using FluentValidation;
using Sentium.Core.Results;
using Sentium.Locus.Common;

namespace Sentium.Locus.Features.Locations;

public sealed class SaveLocationRequestValidator : AbstractValidator<SaveLocationRequest>
{
    public SaveLocationRequestValidator()
    {
        RuleFor(x => x.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Location name is required.")
            .MaximumLength(LocusLimits.LocationNameMax);

        RuleFor(x => x.Description).MaximumLength(LocusLimits.LocationTextMax);
        RuleFor(x => x.AccessNotes).MaximumLength(LocusLimits.LocationTextMax);
        RuleFor(x => x.ParentLocationId).Must(id => id != Guid.Empty).When(x => x.ParentLocationId.HasValue)
            .WithMessage("Parent location id is invalid.");
    }
}
