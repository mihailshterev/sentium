using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Sentium.Identity.Features.Users;
using Sentium.Tests.Unit.Modules.Common;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Identity;

public sealed class UsersControllerTests
{
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly UsersController _controller;

    public UsersControllerTests()
    {
        _controller = new UsersController(_userService, new PrincipalCurrentUser(() => _controller?.User))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "Test"))
                }
            }
        };
    }

    private static UserDto MakeUser(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), "user1", "Test", "User", null, ["Member"]);

    [Fact]
    public async Task GetAllUsers_ReturnsOk_WithPagedResponse()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var users = new List<UserDto> { MakeUser() };
        _userService.GetPagedUsersAsync(1, 20, ct).Returns((users, 1));

        // Act
        var result = await _controller.GetAllUsers(1, 20, ct);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetAllUsers_ClampsPageSize_ToMax100()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        _userService.GetPagedUsersAsync(1, 100, ct).Returns((new List<UserDto>(), 0));

        // Act
        await _controller.GetAllUsers(1, 500, ct);

        // Assert
        await _userService.Received(1).GetPagedUsersAsync(1, 100, ct);
    }

    [Fact]
    public async Task GetAllUsers_ClampsPage_ToMinimum1()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        _userService.GetPagedUsersAsync(1, 20, ct).Returns((new List<UserDto>(), 0));

        // Act
        await _controller.GetAllUsers(0, 20, ct);

        // Assert
        await _userService.Received(1).GetPagedUsersAsync(1, 20, ct);
    }

    [Fact]
    public async Task GetUser_ReturnsOk_WhenUserExists()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var user = MakeUser(id);
        _userService.GetUserByIdAsync(id, ct).Returns(user);

        // Act
        var result = await _controller.GetUser(id, ct);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetUser_ReturnsNotFound_WhenUserMissing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _userService.GetUserByIdAsync(id, ct).Returns((UserDto?)null);

        // Act
        var result = await _controller.GetUser(id, ct);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteUser_ReturnsNoContent_WhenSuccessful()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var requesterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, requesterId.ToString())], "Test"))
            }
        };
        _userService.DeleteUserAsync(requesterId, targetId, ct).Returns((true, Array.Empty<string>()));

        // Act
        var result = await _controller.DeleteUser(targetId, ct);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteUser_ReturnsBadRequest_WhenServiceFails()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var requesterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, requesterId.ToString())], "Test"))
            }
        };
        _userService.DeleteUserAsync(requesterId, targetId, ct)
            .Returns((false, new[] { "Cannot delete the last sovereign." }));

        // Act
        var result = await _controller.DeleteUser(targetId, ct);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DeleteUser_ReturnsUnauthorized_WhenNoSubClaim()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };

        // Act
        var result = await _controller.DeleteUser(Guid.NewGuid(), ct);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }
}
