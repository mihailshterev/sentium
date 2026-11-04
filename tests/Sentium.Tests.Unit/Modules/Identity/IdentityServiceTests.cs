using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Sentium.Identity.Dtos;
using NSubstitute;
using Sentium.Identity.Entities;
using Sentium.Identity.Features.Services;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Identity;

public sealed class IdentityServiceTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IdentityService _service;

    public IdentityServiceTests()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);

        _signInManager = Substitute.For<SignInManager<ApplicationUser>>(
            _userManager,
            Substitute.For<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            null, null, null, null);

        var logger = Substitute.For<ILogger<IdentityService>>();
        _service = new IdentityService(_userManager, _signInManager, logger);
    }

    [Fact]
    public async Task RegisterUserAsync_Success_ReturnsSucceededResultAndSignsIn()
    {
        var request = new RegisterRequest("user1", "Test@123");
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);
        _signInManager.SignInAsync(Arg.Any<ApplicationUser>(), Arg.Any<bool>())
            .Returns(Task.CompletedTask);

        var (result, user) = await _service.RegisterUserAsync(request);

        result.Succeeded.Should().BeTrue();
        user.Should().NotBeNull();
        user!.UserName.Should().Be(request.UserName);
        await _signInManager.Received(1).SignInAsync(user, false);
    }

    [Fact]
    public async Task RegisterUserAsync_Failure_DoesNotSignIn()
    {
        var request = new RegisterRequest("baduser", "weak");
        var failure = IdentityResult.Failed(new IdentityError { Description = "Password too short." });
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(failure);

        var (result, _) = await _service.RegisterUserAsync(request);

        result.Succeeded.Should().BeFalse();
        await _signInManager.DidNotReceive().SignInAsync(Arg.Any<ApplicationUser>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task RegisterUserAsync_NullRequest_ThrowsArgumentNullException()
    {
        var act = async () => await _service.RegisterUserAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task LoginAsync_SuccessfulLogin_ReturnsSucceededSignInResult()
    {
        _signInManager.PasswordSignInAsync("user1", "Test@123", false, true)
            .Returns(SignInResult.Success);

        var result = await _service.LoginAsync("user1", "Test@123");

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task LoginAsync_FailedLogin_ReturnsFailedSignInResult()
    {
        _signInManager.PasswordSignInAsync(Arg.Any<string>(), Arg.Any<string>(), false, true)
            .Returns(SignInResult.Failed);

        var result = await _service.LoginAsync("user1", "wrong");

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task LoginAsync_LockedOut_ReturnsLockedOutResult()
    {
        _signInManager.PasswordSignInAsync(Arg.Any<string>(), Arg.Any<string>(), false, true)
            .Returns(SignInResult.LockedOut);

        var result = await _service.LoginAsync("user1", "wrong");

        result.IsLockedOut.Should().BeTrue();
    }

    [Fact]
    public async Task LogoutAsync_SignsOut()
    {
        _signInManager.SignOutAsync().Returns(Task.CompletedTask);

        await _service.LogoutAsync();

        await _signInManager.Received(1).SignOutAsync();
    }
}

