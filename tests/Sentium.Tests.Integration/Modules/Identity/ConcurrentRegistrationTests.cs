using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Core.Constants;
using Sentium.Identity.Entities;
using Sentium.Identity.Features.Account;
using Sentium.Registry.Contracts;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Identity;

/// <summary>
/// Sign-ups that arrive at the same moment: on a fresh install exactly one of them becomes the Sovereign, and once
/// registration is open none of them fails.
/// </summary>
public sealed class ConcurrentRegistrationTests(ConcurrentRegistrationTests.Factory factory)
    : IClassFixture<ConcurrentRegistrationTests.Factory>
{
    private const string Password = "correct-horse-1";
    private const int ConcurrentSignUps = 12;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SimultaneousSignUps_OneSovereignThenEveryMemberSucceeds()
    {
        var founders = await RegisterAllAsync("founder");

        Assert.Single(founders, status => status == HttpStatusCode.OK);
        Assert.All(founders.Where(status => status != HttpStatusCode.OK), status => Assert.Equal(HttpStatusCode.Forbidden, status));
        Assert.Single(await SovereignsAsync());

        await OpenRegistrationAsync();

        var members = await RegisterAllAsync("member");

        Assert.All(members, status => Assert.Equal(HttpStatusCode.OK, status));
        Assert.Single(await SovereignsAsync());
    }

    private async Task<HttpStatusCode[]> RegisterAllAsync(string prefix)
    {
        var clients = Enumerable.Range(0, ConcurrentSignUps).Select(_ => factory.CreateClient()).ToList();
        try
        {
            var responses = await Task.WhenAll(clients.Select((client, i) =>
                client.PostAsJsonAsync("/api/identity/account/register", new RegisterRequest($"{prefix}-{i}", Password), Ct)));
            return responses.Select(response => response.StatusCode).ToArray();
        }
        finally
        {
            clients.ForEach(client => client.Dispose());
        }
    }

    private async Task<IList<ApplicationUser>> SovereignsAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await users.GetUsersInRoleAsync(SecurityRoles.Sovereign);
    }

    private async Task OpenRegistrationAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        await settings.UpdateAsync(SettingsKeys.Access, null, JsonSerializer.SerializeToElement(new { registrationOpen = true }), "test", Ct);
    }

    /// <summary>
    /// The real authentication, with an auth rate limit high enough for a burst of sign-ups from one address.
    /// </summary>
    public sealed class Factory : MonolithWebApplicationFactory
    {
        protected override bool AuthenticateEveryRequest => false;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Identity:AuthRateLimit:PermitLimit", "1000");
        }
    }
}
