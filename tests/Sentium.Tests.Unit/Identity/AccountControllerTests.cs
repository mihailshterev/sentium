using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Sentium.Identity.Api.Controllers;
using Sentium.Identity.Application.Abstractions;
using Sentium.Identity.Core.Entities;
using Xunit;

namespace Sentium.Tests.Unit.Identity;

public sealed class AccountControllerTests
{
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();
    private readonly IUserService _userManagementService = Substitute.For<IUserService>();
    private readonly AccountController _controller;

    public AccountControllerTests()
    {
        _controller = new AccountController(_identityService, _userManagementService)
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
        var request = new RegisterRequest { Email = "new@test.com", Password = "Test@123" };
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = request.Email, FirstName = "new" };
        _identityService.RegisterUserAsync(request).Returns((IdentityResult.Success, user));

        // Act
        var result = await _controller.Register(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        await _identityService.Received(1).RegisterUserAsync(request);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenRegistrationFails()
    {
        // Arrange
        var request = new RegisterRequest { Email = "bad@test.com", Password = "weak" };
        var failure = IdentityResult.Failed(new IdentityError { Description = "Password too short." });
        _identityService.RegisterUserAsync(request).Returns((failure, (ApplicationUser?)null));

        // Act
        var result = await _controller.Register(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Login_ReturnsOk_WhenLoginIsSuccessful()
    {
        // Arrange
        var request = new LoginRequest { Email = "user@test.com", Password = "Test@123" };
        _identityService.LoginAsync(request.Email, request.Password)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        // Act
        var result = await _controller.Login(request, returnUrl: null);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Login_ReturnsLockedStatus_WhenUserIsLockedOut()
    {
        // Arrange
        var request = new LoginRequest { Email = "locked@test.com", Password = "wrong" };
        _identityService.LoginAsync(request.Email, request.Password)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.LockedOut);

        // Act
        var result = await _controller.Login(request, returnUrl: null);

        // Assert
        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status423Locked);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenLoginFails()
    {
        // Arrange
        var request = new LoginRequest { Email = "user@test.com", Password = "wrong" };
        _identityService.LoginAsync(request.Email, request.Password)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        // Act
        var result = await _controller.Login(request, returnUrl: null);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }
}
