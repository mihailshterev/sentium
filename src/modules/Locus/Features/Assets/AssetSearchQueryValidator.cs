using FluentValidation;
using Sentium.Core.Results;
using Sentium.Locus.Common;

namespace Sentium.Locus.Features.Assets;

public sealed class AssetSearchQueryValidator : AbstractValidator<AssetSearchQuery>
{
    public AssetSearchQueryValidator()
    {
        RuleFor(x => x.Q).MaximumLength(LocusLimits.SearchQueryMax);
        RuleFor(x => x.Category).MaximumLength(LocusLimits.CategoryMax);
        RuleFor(x => x.Tags)
            .Must(t => t is null || t.Count <= LocusLimits.MaxTagsPerAsset)
            .WithMessage($"Filter by at most {LocusLimits.MaxTagsPerAsset} tags.");
        RuleForEach(x => x.Tags).MaximumLength(LocusLimits.TagMax);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PaginationQuery.MaxPageSize);
    }
}
