using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Results;
using Sentium.Identity.Features.Avatars;
using Sentium.Identity.Features.Security;
using Sentium.Infrastructure.Security;

namespace Sentium.Identity.Features.Users;

[ApiController]
[Route($"{IdentityRoutes.Prefix}/users")]
[Authorize]
internal sealed class UsersController(IUserService userService, ICurrentUser currentUser) : ControllerBase
{
    /// <param name="page">1-based.</param>
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

    /// <response code="400">The target is the requester or the last Sovereign.</response>
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

    /// <summary>
    /// Sets a new password for another user who forgot theirs, unlocks the account and signs it out everywhere.
    /// </summary>
    /// <response code="400">If the target is the requester (use the Profile page) or the password breaks the rules.</response>
    [HttpPut("{id:guid}/password")]
    [Authorize(Roles = Roles.Sovereign)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } requesterId)
        {
            return Unauthorized();
        }

        if (requesterId == id)
        {
            return Problem(
                detail: "Change your own password on your Profile page.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (await userService.GetUserByIdAsync(id, ct) is null)
        {
            return NotFound();
        }

        var (succeeded, errors) = await userService.ResetPasswordAsync(id, request.NewPassword, ct);
        return succeeded
            ? NoContent()
            : Problem(detail: string.Join(" ", errors), statusCode: StatusCodes.Status400BadRequest);
    }

    /// <summary>
    /// Lifts a lockout caused by repeated failed sign-ins.
    /// </summary>
    [HttpPost("{id:guid}/unlock")]
    [Authorize(Roles = Roles.Sovereign)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unlock(Guid id, CancellationToken ct)
    {
        return await userService.UnlockAsync(id, ct) ? NoContent() : NotFound();
    }

    private static UserResponse ToResponse(UserDto user) =>
        new(user.Id, user.UserName!, user.FirstName, user.LastName, [.. user.Roles], user.IsLockedOut, AvatarUrls.For(user.Id, user.AvatarUpdatedAt));
}
