using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sentium.Identity.Core.Entities;
using Sentium.Identity.Infrastructure.Identity;
using Sentium.Tests.Unit.Common;
using Xunit;

namespace Sentium.Tests.Unit.Identity;

public sealed class IdentityServiceTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly SpyEventBus _eventBus = new();
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
        _service = new IdentityService(_userManager, _signInManager, _eventBus, logger);
    }

    [Fact]
    public async Task RegisterUserAsync_Success_ReturnsSucceededResultAndPublishesEvent()
    {
        var request = new RegisterRequest { Email = "user@test.com", Password = "Test@123" };
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);
        _signInManager.SignInAsync(Arg.Any<ApplicationUser>(), Arg.Any<bool>())
            .Returns(Task.CompletedTask);

        var (result, user) = await _service.RegisterUserAsync(request);

        result.Succeeded.Should().BeTrue();
        user.Should().NotBeNull();
        user!.Email.Should().Be(request.Email);
        _eventBus.PublishedSubjects.Should().ContainSingle(s => s == "identity.user.registered");
    }

    [Fact]
    public async Task RegisterUserAsync_Failure_DoesNotPublishEvent()
    {
        var request = new RegisterRequest { Email = "bad@test.com", Password = "weak" };
        var failure = IdentityResult.Failed(new IdentityError { Description = "Password too short." });
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(failure);

        var (result, _) = await _service.RegisterUserAsync(request);

        result.Succeeded.Should().BeFalse();
        _eventBus.PublishedSubjects.Should().BeEmpty();
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
        _signInManager.PasswordSignInAsync("user@test.com", "Test@123", false, true)
            .Returns(SignInResult.Success);

        var result = await _service.LoginAsync("user@test.com", "Test@123");

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task LoginAsync_FailedLogin_ReturnsFailedSignInResult()
    {
        _signInManager.PasswordSignInAsync(Arg.Any<string>(), Arg.Any<string>(), false, true)
            .Returns(SignInResult.Failed);

        var result = await _service.LoginAsync("user@test.com", "wrong");

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task LoginAsync_LockedOut_ReturnsLockedOutResult()
    {
        _signInManager.PasswordSignInAsync(Arg.Any<string>(), Arg.Any<string>(), false, true)
            .Returns(SignInResult.LockedOut);

        var result = await _service.LoginAsync("user@test.com", "wrong");

        result.IsLockedOut.Should().BeTrue();
    }
}

