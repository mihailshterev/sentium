using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Locus.Features.Assets;

namespace Sentium.Locus.Features.Assets;

/// <summary>
/// Tag and category suggestions for filters and autocomplete.
/// </summary>
[ApiController]
[Authorize]
[Route(LocusRoutes.Prefix)]
public sealed class TaxonomyController(IAssetService assetService) : ControllerBase
{
    /// <summary>
    /// Most used tags (top 50), optionally only those starting with <paramref name="prefix"/>.
    /// </summary>
    [HttpGet("tags")]
    [ProducesResponseType(typeof(IReadOnlyList<TagCountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTags([FromQuery] string? prefix, CancellationToken ct)
        => Ok(await assetService.GetTagsAsync(prefix, ct));

    /// <summary>
    /// Every category in use, alphabetically.
    /// </summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
        => Ok(await assetService.GetCategoriesAsync(ct));
}
