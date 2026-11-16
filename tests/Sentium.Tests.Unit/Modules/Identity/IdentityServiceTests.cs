using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sentium.Identity.Entities;
using Sentium.Identity.Features.Account;
using Sentium.Registry.Contracts;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Identity;

/// <summary>
/// Sign-in flows. Registration runs in a database transaction and is covered by the integration tests.
/// </summary>
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
        _service = new IdentityService(_userManager, _signInManager, null!, Substitute.For<ISettingsService>(), logger);
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

