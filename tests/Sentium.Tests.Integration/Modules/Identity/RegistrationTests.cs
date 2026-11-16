using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
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
/// On a fresh database: the first account becomes the Sovereign, after which registration is closed until a
/// Sovereign opens it, and accounts registered then are Members.
/// </summary>
public sealed class RegistrationTests(RealAuthWebApplicationFactory factory) : IClassFixture<RealAuthWebApplicationFactory>
{
    private const string Password = "correct-horse-1";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FirstAccountBecomesSovereign_ThenRegistrationIsClosedUntilOpened()
    {
        using var client = factory.CreateClient();

        (await StatusAsync(client)).Should().Be(new RegistrationStatusResponse(Open: true, Bootstrap: true));

        (await RegisterAsync(client, "founder")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await RolesOfAsync("founder")).Should().BeEquivalentTo(SecurityRoles.Member, SecurityRoles.Sovereign);

        (await StatusAsync(client)).Should().Be(new RegistrationStatusResponse(Open: false, Bootstrap: false));
        (await RegisterAsync(client, "visitor")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await OpenRegistrationAsync();

        (await RegisterAsync(client, "member")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await RolesOfAsync("member")).Should().BeEquivalentTo(SecurityRoles.Member);
    }

    private static async Task<RegistrationStatusResponse> StatusAsync(HttpClient client)
    {
        return (await client.GetFromJsonAsync<RegistrationStatusResponse>("/api/identity/account/registration", Ct))!;
    }

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string userName)
    {
        return client.PostAsJsonAsync("/api/identity/account/register", new RegisterRequest(userName, Password), Ct);
    }

    private async Task<IList<string>> RolesOfAsync(string userName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByNameAsync(userName);
        return await users.GetRolesAsync(user!);
    }

    private async Task OpenRegistrationAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        await settings.UpdateAsync(SettingsKeys.Access, null, JsonSerializer.SerializeToElement(new { registrationOpen = true }), "test", Ct);
    }
}
