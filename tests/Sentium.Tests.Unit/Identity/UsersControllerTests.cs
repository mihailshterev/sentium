using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Sentium.Identity.Api.Controllers;
using Sentium.Identity.Application.Abstractions;
using Sentium.Identity.Application.Users;
using System.Security.Claims;
using Xunit;

namespace Sentium.Tests.Unit.Identity;

public sealed class UsersControllerTests
{
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IUserClaimsService _claimsService = Substitute.For<IUserClaimsService>();
    private readonly UsersController _controller;

    public UsersControllerTests()
    {
        _controller = new UsersController(_userService, _claimsService)
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
        new(id ?? Guid.NewGuid(), "user@test.com", "Test", "User", null);

    [Fact]
    public async Task GetAllUsers_ReturnsOk_WithPagedResponse()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var users = new List<UserDto> { MakeUser() };
        _userService.GetPagedUsersAsync(1, 20, ct).Returns((users, 1));
        _claimsService.GetBatchClaimsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<IEnumerable<string>>(), ct)
            .Returns(new Dictionary<Guid, IReadOnlyCollection<Claim>>());

        // Act
        var result = await _controller.GetAllUsers(1, 20, ct);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetAllUsers_ClampsPageSize_ToMax100()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        _userService.GetPagedUsersAsync(1, 100, ct).Returns((new List<UserDto>(), 0));
        _claimsService.GetBatchClaimsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<IEnumerable<string>>(), ct)
            .Returns(new Dictionary<Guid, IReadOnlyCollection<Claim>>());

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
        _claimsService.GetBatchClaimsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<IEnumerable<string>>(), ct)
            .Returns(new Dictionary<Guid, IReadOnlyCollection<Claim>>());

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
        _claimsService.GetClaimsAsync(id, Arg.Any<IEnumerable<string>>(), ct)
            .Returns(new List<Claim>());

        // Act
        var result = await _controller.GetUser(id, ct);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
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
        result.Should().BeOfType<NotFoundResult>();
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
        result.Should().BeOfType<NoContentResult>();
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
        result.Should().BeOfType<BadRequestObjectResult>();
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
        result.Should().BeOfType<UnauthorizedResult>();
    }
}
