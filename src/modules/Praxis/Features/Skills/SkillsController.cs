using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Results;

namespace Sentium.Praxis.Features.Skills;

[ApiController]
[Authorize]
[Route($"{PraxisRoutes.Prefix}/skills")]
internal sealed class SkillsController(
    IAgentSkillService skillService,
    IBuiltInSkillCatalog builtInSkillCatalog) : ControllerBase
{
    private const long MaxUploadBytes = 500 * 1024;

    [HttpGet("built-in")]
    [ProducesResponseType(typeof(IEnumerable<BuiltInSkillInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetBuiltInSkills()
    {
        var skills = builtInSkillCatalog.GetAll();
        return Ok(skills);
    }

    /// <param name="page">1-based.</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<AgentSkillDto>), StatusCodes.Status200OK)]
    public async ValueTask<IActionResult> GetSkills(
        [FromQuery] AgentSkillType? skillType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationQuery.DefaultPageSize,
        CancellationToken ct = default)
    {
        var skills = await skillService.GetPagedAsync(skillType, page, pageSize, ct);
        return Ok(skills);
    }

    [HttpGet("{skillId:guid}")]
    [ProducesResponseType(typeof(AgentSkillDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<IActionResult> GetSkillById(Guid skillId, CancellationToken ct)
    {
        var skill = await skillService.GetByIdAsync(skillId, ct);
        return skill is null ? NotFound() : Ok(skill);
    }

    [HttpPost]
    [ProducesResponseType(typeof(AgentSkillDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async ValueTask<IActionResult> CreateSkill([FromBody] CreateAgentSkillRequest request, CancellationToken ct)
    {
        var result = await skillService.CreateAsync(request, ct);
        if (result.Status == ResultStatus.Conflict)
        {
            return Conflict(new ProblemDetails { Title = "Conflict", Detail = result.Error, Status = StatusCodes.Status409Conflict });
        }

        return CreatedAtAction(nameof(GetSkillById), new { skillId = result.Value!.Id }, result.Value);
    }

    /// <summary>
    /// Creates a skill from a Markdown file whose content becomes its instructions.
    /// </summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(AgentSkillDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async ValueTask<IActionResult> UploadSkill(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("No file provided.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (!extension.Equals(".md", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Only .md (Markdown) files are supported for upload.");
        }

        if (file.Length > MaxUploadBytes)
        {
            return BadRequest("File exceeds the 500 KB limit.");
        }

        using var reader = new StreamReader(file.OpenReadStream());
        var content = await reader.ReadToEndAsync(ct);

        var rawName = Path.GetFileNameWithoutExtension(file.FileName);
        var skillName = SlugifyName(rawName);

        var request = new CreateAgentSkillRequest(
            Name: skillName,
            Description: $"Uploaded skill from {file.FileName}",
            Instructions: content,
            SkillType: AgentSkillType.Uploaded,
            FileName: file.FileName);

        var result = await skillService.CreateAsync(request, ct);
        if (result.Status == ResultStatus.Conflict)
        {
            return Conflict(new ProblemDetails { Title = "Conflict", Detail = result.Error, Status = StatusCodes.Status409Conflict });
        }

        return CreatedAtAction(nameof(GetSkillById), new { skillId = result.Value!.Id }, result.Value);
    }

    [HttpPut("{skillId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<IActionResult> UpdateSkill(Guid skillId, [FromBody] UpdateAgentSkillRequest request, CancellationToken ct)
    {
        var updated = await skillService.UpdateAsync(skillId, request, ct);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{skillId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<IActionResult> DeleteSkill(Guid skillId, CancellationToken ct)
    {
        var deleted = await skillService.DeleteAsync(skillId, ct);
        return deleted ? NoContent() : NotFound();
    }

    private static string SlugifyName(string name)
    {
        var slug = name
            .ToLowerInvariant()
            .Replace(' ', '-')
            .Replace('_', '-');

        slug = new string([.. slug.Where(c => char.IsLetterOrDigit(c) || c == '-')]);

        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        slug = slug.Trim('-');

        return slug.Length == 0 ? "custom-skill" : slug[..Math.Min(64, slug.Length)];
    }
}
