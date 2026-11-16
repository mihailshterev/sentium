using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Results;
using Sentium.Identity.Features.Security;
using Sentium.Infrastructure.Security;

namespace Sentium.Identity.Features.Users;

[ApiController]
[Route($"{IdentityRoutes.Prefix}/users")]
[Authorize]
internal sealed class UsersController(IUserService userService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Lists users registered in the system, with pagination.
    /// </summary>
    /// <remarks>
    /// Only accessible by users with the Sovereign role.
    /// </remarks>
    /// <param name="page">1-based page number (default: 1).</param>
    /// <param name="pageSize">Number of items per page (default: 20, max: 100).</param>
    /// <response code="200">Returns a paginated list of users with their basic profiles and roles.</response>
    [HttpGet]
    [Authorize(Roles = Roles.Sovereign)]
    [ProducesResponseType(typeof(PagedResponse<UserResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllUsers([FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, CancellationToken ct = default)
    {
        (page, pageSize) = new PaginationQuery { Page = page, PageSize = pageSize }.Normalize();

        var (users, totalCount) = await userService.GetPagedUsersAsync(page, pageSize, ct);
        var items = users.Select(ToResponse).ToList();

        return Ok(PagedResponse<UserResponse>.Create(items, totalCount, page, pageSize));
    }

    /// <summary>
    /// Retrieves detailed information for a specific user.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = Roles.Sovereign)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUser(Guid id, CancellationToken ct)
    {
        var user = await userService.GetUserByIdAsync(id, ct);
        if (user is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(user));
    }

    /// <summary>
    /// Permanently deletes a user from the system.
    /// </summary>
    /// <remarks>
    /// Only accessible by Sovereign.
    /// This operation is destructive and includes a hierarchy check to prevent self-deletion or unauthorized escalation.
    /// </remarks>
    /// <response code="400">If the deletion violates business rules (e.g., deleting yourself).</response>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Sovereign)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
    {
        var requesterId = currentUser.UserId;
        if (requesterId is null)
        {
            return Unauthorized();
        }

        var (succeeded, errors) = await userService.DeleteUserAsync(requesterId.Value, id, ct);
        if (!succeeded)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["user"] = errors
            }));
        }

        return NoContent();
    }

    private static UserResponse ToResponse(UserDto user) =>
        new(user.Id, user.UserName!, user.FirstName, user.LastName, [.. user.Roles], user.IsLockedOut);
}
