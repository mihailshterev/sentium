using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Security;
using Xunit;

namespace Sentium.Tests.Unit.Infrastructure.Security;

/// <summary>
/// Background work runs either as the system (every row) or as one user (that user's rows only).
/// </summary>
public sealed class CurrentUserTests : IDisposable
{
    private readonly ServiceProvider _services;

    public CurrentUserTests()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddHybridCache();
        builder.AddSentiumPlatform();
        _services = builder.Services.BuildServiceProvider();
    }

    public void Dispose() => _services.Dispose();

    [Fact]
    public void ActAs_ScopesToThatUser_WithoutAnyBypass()
    {
        using var scope = _services.CreateScope();
        var userId = Guid.NewGuid();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().ActAs(userId);

        var user = scope.ServiceProvider.GetRequiredService<ICurrentUser>();

        Assert.Equal(userId, user.UserId);
        Assert.True(user.IsAuthenticated);
        Assert.False(user.IsSystem);
        Assert.False(user.IsSovereign);
    }

    [Fact]
    public void Activate_IsSystem_WithoutAUser()
    {
        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();

        var user = scope.ServiceProvider.GetRequiredService<ICurrentUser>();

        Assert.True(user.IsSystem);
        Assert.Null(user.UserId);
    }
}
