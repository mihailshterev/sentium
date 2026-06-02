using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentium.Identity.Core.Entities;
using Sentium.Identity.Core.Security;
using Sentium.Identity.Infrastructure.Identity;
using Xunit;

namespace Sentium.Tests.Unit.Identity;

public sealed class UserClaimsServiceTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly UserClaimsService _service;

    public UserClaimsServiceTests()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        _service = new UserClaimsService(_userManager, new PassThroughHybridCache(), NullLogger<UserClaimsService>.Instance);
    }

    [Fact]
    public async Task GetClaimsAsync_UserExists_ReturnsBaseClaims()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "user@test.com",
            Email = "user@test.com",
            FirstName = "Test",
            LastName = "User"
        };
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetClaimsAsync(user).Returns([]);
        _userManager.GetRolesAsync(user).Returns([]);

        var claims = await _service.GetClaimsAsync(userId, [Scopes.OpenId], TestContext.Current.CancellationToken);

        claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == userId.ToString());
        claims.Should().Contain(c => c.Type == ClaimTypes.Email && c.Value == user.Email);
    }

    [Fact]
    public async Task GetClaimsAsync_WithRolesScope_IncludesRoleClaims()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, UserName = "admin@test.com", Email = "admin@test.com", FirstName = "Admin" };
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetClaimsAsync(user).Returns([]);
        _userManager.GetRolesAsync(user).Returns(["Admin", "User"]);

        var claims = await _service.GetClaimsAsync(userId, [Scopes.Roles], TestContext.Current.CancellationToken);

        claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");
        claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "User");
    }

    [Fact]
    public async Task GetClaimsAsync_WithoutRolesScope_DoesNotIncludeRoleClaims()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, UserName = "user@test.com", Email = "user@test.com", FirstName = "User" };
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetClaimsAsync(user).Returns([]);

        var claims = await _service.GetClaimsAsync(userId, [Scopes.OpenId, Scopes.Profile], TestContext.Current.CancellationToken);

        claims.Should().NotContain(c => c.Type == ClaimTypes.Role);
        _ = _userManager.DidNotReceive().GetRolesAsync(Arg.Any<ApplicationUser>());
    }

    [Fact]
    public async Task GetClaimsAsync_UserNotFound_ThrowsInvalidOperationException()
    {
        var unknownId = Guid.NewGuid();
        _userManager.FindByIdAsync(unknownId.ToString()).Returns((ApplicationUser?)null);

        var act = async () => await _service.GetClaimsAsync(unknownId, [Scopes.OpenId], TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{unknownId}*");
    }
}
