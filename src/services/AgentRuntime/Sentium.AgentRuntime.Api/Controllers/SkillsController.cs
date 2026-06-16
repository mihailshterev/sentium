using Sentium.AgentRuntime.Core.Skills;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Shared.Results;

namespace Sentium.AgentRuntime.Api.Controllers;

/// <summary>
/// Provides endpoints for managing and retrieving agent skills, including built-in and user-defined skills.
/// </summary>
[ApiController]
[Authorize]
[Route("skills")]
public sealed class SkillsController(
    IAgentSkillService skillService,
    IBuiltInSkillCatalog builtInSkillCatalog) : ControllerBase
{
    /// <summary>
    /// Retrieves a list of all built-in skills available in the system catalog.
    /// </summary>
    /// <returns>A collection of information descriptors for built-in skills.</returns>
    /// <response code="200">Returns the list of built-in skill descriptors.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet("built-in")]
    [ProducesResponseType(typeof(IEnumerable<BuiltInSkillInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetBuiltInSkills()
    {
        var skills = builtInSkillCatalog.GetAll();
        return Ok(skills);
    }

    /// <summary>
    /// Retrieves a page of custom agent skills, optionally filtered by skill type.
    /// </summary>
    /// <param name="skillType">Optional skill-type filter (Custom or Uploaded).</param>
    /// <param name="page">1-based page number (default: 1).</param>
    /// <param name="pageSize">Number of items per page (default: 20, max: 100).</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A paginated collection of agent skill data transfer objects.</returns>
    /// <response code="200">Returns the page of custom skills.</response>
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

    /// <summary>
    /// Retrieves a specific agent skill by its unique identifier.
    /// </summary>
    /// <param name="skillId">The unique ID of the skill.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The data transfer object for the requested skill.</returns>
    /// <response code="200">Returns the requested skill.</response>
    /// <response code="404">If a skill with the specified ID does not exist.</response>
    [HttpGet("{skillId:guid}")]
    [ProducesResponseType(typeof(AgentSkillDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<IActionResult> GetSkillById(Guid skillId, CancellationToken ct)
    {
        var skill = await skillService.GetByIdAsync(skillId, ct);
        return skill is null ? NotFound() : Ok(skill);
    }

    /// <summary>
    /// Creates a new custom agent skill.
    /// </summary>
    /// <param name="request">The data required to create the skill.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The created skill data.</returns>
    /// <response code="201">Returns the newly created skill.</response>
    /// <response code="400">If the request is invalid.</response>
    /// <response code="409">If a skill with the same name already exists.</response>
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
    /// Creates a new skill by uploading a Markdown (.md) file.
    /// </summary>
    /// <param name="file">The Markdown file containing the skill instructions.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The created skill data.</returns>
    /// <response code="201">Returns the newly created skill.</response>
    /// <response code="400">If the file is invalid, empty, or not a Markdown file.</response>
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

        if (file.Length > 500 * 1024)
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

    /// <summary>
    /// Updates the details of an existing agent skill.
    /// </summary>
    /// <param name="skillId">The ID of the skill to update.</param>
    /// <param name="request">The updated skill details.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>No content if the update was successful.</returns>
    /// <response code="204">Skill updated successfully.</response>
    /// <response code="400">If the request is null or invalid.</response>
    /// <response code="404">If the skill was not found.</response>
    [HttpPut("{skillId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<IActionResult> UpdateSkill(Guid skillId, [FromBody] UpdateAgentSkillRequest request, CancellationToken ct)
    {
        var updated = await skillService.UpdateAsync(skillId, request, ct);
        return updated ? NoContent() : NotFound();
    }

    /// <summary>
    /// Permanently deletes an agent skill.
    /// </summary>
    /// <param name="skillId">The ID of the skill to delete.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>No content if the deletion was successful.</returns>
    /// <response code="204">Skill deleted successfully.</response>
    /// <response code="404">If the skill was not found.</response>
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
