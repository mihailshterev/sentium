using System.Text;
using Microsoft.EntityFrameworkCore;
using Sentium.Locus.Entities;
using Sentium.Locus.Features.Locations;

namespace Sentium.Locus.Features.Assets;

/// <summary>
/// Free-text search over assets. Every asset carries a lower-cased <see cref="Asset.SearchText"/>
/// (name, category, maker, model, serial, description, tags), so each search word is one
/// <c>LIKE '%word%'</c> - cheap at household scale and, unlike full-text stemming, it matches partial words
/// and model numbers such as "wh-1000". A word also matches when it names a containing location, so
/// "garage drill" finds the drill on the garage shelf.
/// </summary>
public static class AssetSearch
{
    public const char EscapeCharacter = '\\';

    private static readonly string Escape = EscapeCharacter.ToString();

    /// <summary>
    /// Splits a query into at most <see cref="LocusLimits.MaxSearchTerms"/> distinct lower-case words.
    /// </summary>
    public static IReadOnlyList<string> ParseTerms(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var text = query.Trim();
        if (text.Length > LocusLimits.SearchQueryMax)
        {
            text = text[..LocusLimits.SearchQueryMax];
        }

        return [.. text
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Take(LocusLimits.MaxSearchTerms)];
    }

    /// <summary>
    /// <c>%term%</c> with the LIKE wildcards in <paramref name="term"/> escaped, so "100%" matches literally.
    /// </summary>
    public static string ToContainsPattern(string term)
    {
        ArgumentNullException.ThrowIfNull(term);

        var builder = new StringBuilder(term.Length + 2).Append('%');
        foreach (var c in term)
        {
            if (c is '%' or '_' or EscapeCharacter)
            {
                builder.Append(EscapeCharacter);
            }

            builder.Append(c);
        }

        return builder.Append('%').ToString();
    }

    /// <summary>
    /// ANDs one condition per term: the term appears in the asset's search text, or the asset sits in (or
    /// below) a location whose name contains it.
    /// </summary>
    public static IQueryable<Asset> ApplyTerms(IQueryable<Asset> query, IReadOnlyList<string> terms, LocationTree tree)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(tree);

        foreach (var term in terms)
        {
            var pattern = ToContainsPattern(term);
            var locationIds = tree.FindSubtreesMatching(term).ToList();

            query = locationIds.Count == 0
                ? query.Where(a => EF.Functions.Like(a.SearchText, pattern, Escape))
                : query.Where(a => EF.Functions.Like(a.SearchText, pattern, Escape) || locationIds.Contains(a.LocationId));
        }

        return query;
    }

    /// <summary>
    /// Builds the value stored in <see cref="Asset.SearchText"/>.
    /// </summary>
    public static string BuildSearchText(Asset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var parts = new[]
        {
            asset.DisplayName,
            asset.Category,
            asset.Manufacturer,
            asset.ModelNumber,
            asset.SerialNumber,
            asset.PhysicalDescription,
        }.Concat(asset.Tags);

        return string.Join('\n', parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim())).ToLowerInvariant();
    }
}
