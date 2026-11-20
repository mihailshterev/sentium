using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Sentium.Identity.Entities;
using Sentium.Identity.Features.Account;
using Sentium.Identity.Features.Users;
using Sentium.Tests.Unit.Modules.Common;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Identity;

public sealed class AccountControllerTests
{
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();
    private readonly IUserService _userManagementService = Substitute.For<IUserService>();
    private readonly AccountController _controller;

    public AccountControllerTests()
    {
        _controller = new AccountController(_identityService, _userManagementService, new PrincipalCurrentUser(() => _controller?.User))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public async Task Register_ReturnsOk_WhenRegistrationIsSuccessful()
    {
        // Arrange
        var request = new RegisterRequest("newuser", "Test@123");
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = request.UserName, FirstName = "new" };
        _identityService.RegisterUserAsync(request, Arg.Any<CancellationToken>()).Returns(new RegistrationOutcome(IdentityResult.Success, user));

        // Act
        var result = await _controller.Register(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        await _identityService.Received(1).RegisterUserAsync(request, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenRegistrationFails()
    {
        // Arrange
        var request = new RegisterRequest("baduser", "weak");
        var failure = IdentityResult.Failed(new IdentityError { Description = "Password too short." });
        _identityService.RegisterUserAsync(request, Arg.Any<CancellationToken>()).Returns(new RegistrationOutcome(failure, null));

        // Act
        var result = await _controller.Register(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Register_ReturnsForbidden_WhenRegistrationIsClosed()
    {
        var request = new RegisterRequest("newcomer", "Test@1234");
        _identityService.RegisterUserAsync(request, Arg.Any<CancellationToken>()).Returns(RegistrationOutcome.ClosedToNewAccounts);

        var result = await _controller.Register(request, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task Login_ReturnsOk_WhenLoginIsSuccessful()
    {
        // Arrange
        var request = new LoginRequest("user1", "Test@123");
        _identityService.LoginAsync(request.UserName, request.Password)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        // Act
        var result = await _controller.Login(request, returnUrl: null);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Login_ReturnsLockedStatus_WhenUserIsLockedOut()
    {
        // Arrange
        var request = new LoginRequest("lockeduser", "wrong");
        _identityService.LoginAsync(request.UserName, request.Password)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.LockedOut);

        // Act
        var result = await _controller.Login(request, returnUrl: null);

        // Assert
        Assert.Equal(StatusCodes.Status423Locked, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenLoginFails()
    {
        // Arrange
        var request = new LoginRequest("user1", "wrong");
        _identityService.LoginAsync(request.UserName, request.Password)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        // Act
        var result = await _controller.Login(request, returnUrl: null);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Logout_SignsOutAndReturnsNoContent()
    {
        // Act
        var result = await _controller.Logout();

        // Assert
        Assert.IsType<NoContentResult>(result);
        await _identityService.Received(1).LogoutAsync();
    }

    [Fact]
    public void GetSession_ReturnsSubjectNameAndRolesFromClaims()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Name, "user1"),
                    new Claim(ClaimTypes.Role, "Member"),
                    new Claim(ClaimTypes.Role, "Member"),
                    new Claim(ClaimTypes.Role, "Sovereign")
                ], "Test"))
            }
        };

        // Act
        var result = _controller.GetSession();

        // Assert
        var session = Assert.IsType<SessionResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(userId.ToString(), session.Sub);
        Assert.Equal("user1", session.Name);
        Assert.Equivalent(new string[] { "Member", "Sovereign" }, session.Roles);
    }

    [Fact]
    public async Task GetProfile_ReturnsOk_WhenUserFound()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var user = new UserDto(userId, "user1", "Test", "User", null, []);
        _userManagementService.GetUserByIdAsync(userId, ct).Returns(user);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Test"))
            }
        };

        // Act
        var result = await _controller.GetProfile(ct);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetProfile_ReturnsNotFound_WhenUserDeleted()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        _userManagementService.GetUserByIdAsync(userId, ct).Returns((UserDto?)null);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Test"))
            }
        };

        // Act
        var result = await _controller.GetProfile(ct);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetProfile_ReturnsUnauthorized_WhenNoSubClaim()
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
        var result = await _controller.GetProfile(ct);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task UpdateProfile_ReturnsNoContent_WhenSuccessful()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var request = new UpdateProfileRequest("Jane", "Doe", "jane");
        _userManagementService.UpdateProfileAsync(userId, request.FirstName, request.LastName, request.UserName, ct)
            .Returns((true, Array.Empty<string>()));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Test"))
            }
        };

        // Act
        var result = await _controller.UpdateProfile(request, ct);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task UpdateProfile_ReturnsBadRequest_WhenValidationFails()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var request = new UpdateProfileRequest("Jane", "Doe", "taken");
        _userManagementService.UpdateProfileAsync(userId, request.FirstName, request.LastName, request.UserName, ct)
            .Returns((false, new[] { "Username is already in use." }));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Test"))
            }
        };

        // Act
        var result = await _controller.UpdateProfile(request, ct);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }
}
