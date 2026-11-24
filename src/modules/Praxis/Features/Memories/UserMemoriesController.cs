using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Infrastructure.Security;

namespace Sentium.Praxis.Features.Memories;

/// <summary>
/// The signed-in user's saved memories: what agents remember about them. Private to the user, Sovereigns included.
/// </summary>
[ApiController]
[Authorize]
[Route($"{PraxisRoutes.Prefix}/memories")]
internal sealed class UserMemoriesController(IUserMemoryService memories, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Every memory, most recently changed first.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserMemoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMemories(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await memories.ListAsync(userId, ct));
    }

    /// <summary>
    /// Saves a memory. Content that restates an existing memory replaces it instead of adding a duplicate.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserMemoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddMemory([FromBody] SaveUserMemoryRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await memories.RememberAsync(userId, request.Content, ct);
        return Ok(result.Memory);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UserMemoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMemory(Guid id, [FromBody] SaveUserMemoryRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var updated = await memories.UpdateAsync(userId, id, request.Content, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>
    /// Forgets a memory: agents no longer recall it.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMemory(Guid id, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        return await memories.ForgetAsync(userId, id, ct) ? NoContent() : NotFound();
    }
}
