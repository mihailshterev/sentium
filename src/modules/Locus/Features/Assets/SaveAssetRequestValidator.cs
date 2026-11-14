using FluentValidation;
using Sentium.Locus.Common;

namespace Sentium.Locus.Features.Assets;

internal sealed class SaveAssetRequestValidator : AbstractValidator<SaveAssetRequest>
{
    public SaveAssetRequestValidator()
    {
        RuleFor(x => x.DisplayName)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Asset name is required.")
            .MaximumLength(LocusLimits.AssetNameMax);

        RuleFor(x => x.LocationId).NotEmpty().WithMessage("Choose a location for the asset.");
        RuleFor(x => x.Category).MaximumLength(LocusLimits.CategoryMax);
        RuleFor(x => x.PhysicalDescription).MaximumLength(LocusLimits.DescriptionMax);
        RuleFor(x => x.Instructions).MaximumLength(LocusLimits.InstructionsMax);
        RuleFor(x => x.Manufacturer).MaximumLength(LocusLimits.ManufacturerMax);
        RuleFor(x => x.ModelNumber).MaximumLength(LocusLimits.IdentifierMax);
        RuleFor(x => x.SerialNumber).MaximumLength(LocusLimits.IdentifierMax);
        RuleFor(x => x.WarrantyInfo).MaximumLength(LocusLimits.WarrantyMax);
        RuleFor(x => x.AgentInstructions).MaximumLength(LocusLimits.AgentInstructionsMax);

        RuleFor(x => x.Tags)
            .Must(t => t is null || t.Count <= LocusLimits.MaxTagsPerAsset)
            .WithMessage($"An asset can have at most {LocusLimits.MaxTagsPerAsset} tags.");

        RuleForEach(x => x.Tags)
            .Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Tags cannot be empty.")
            .Must(t => t is null || t.Trim().Length <= LocusLimits.TagMax).WithMessage($"Tags can be at most {LocusLimits.TagMax} characters.")
            .Must(t => t is null || !t.Contains(',', StringComparison.Ordinal)).WithMessage("Tags cannot contain commas.");

        RuleFor(x => x.LastServicedDate)
            .GreaterThanOrEqualTo(x => x.PurchaseDate)
            .When(x => x.PurchaseDate.HasValue && x.LastServicedDate.HasValue)
            .WithMessage("The last service date cannot be before the purchase date.");
    }
}
