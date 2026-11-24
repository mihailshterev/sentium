using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Core.Constants;
using Sentium.Identity.Entities;
using Sentium.Identity.Features.Account;
using Sentium.Identity.Features.Users;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Identity;

/// <summary>
/// Changing and resetting passwords, unlocking accounts and ending sessions, over the real session cookie.
/// </summary>
public sealed class PasswordManagementTests(PasswordManagementTests.Factory factory) : IClassFixture<PasswordManagementTests.Factory>
{
    private const string Password = "correct-horse-1";
    private const string NewPassword = "battery-staple-2";
    private const int FailedAttemptsBeforeLockout = 5;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ChangePassword_KeepsThisSession_EndsTheOthers()
    {
        var userName = await CreateUserAsync(SecurityRoles.Member);
        using var here = await SignInAsync(userName, Password);
        using var elsewhere = await SignInAsync(userName, Password);

        var wrong = await here.PutAsJsonAsync("/api/identity/account/me/password", new ChangePasswordRequest("not-it-1", NewPassword), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var changed = await here.PutAsJsonAsync("/api/identity/account/me/password", new ChangePasswordRequest(Password, NewPassword), Ct);
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await here.GetAsync("/api/identity/account/me", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await elsewhere.GetAsync("/api/identity/account/me", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(NewClient(), userName, Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(NewClient(), userName, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task SignOutOtherSessions_KeepsThisSession()
    {
        var userName = await CreateUserAsync(SecurityRoles.Member);
        using var here = await SignInAsync(userName, Password);
        using var elsewhere = await SignInAsync(userName, Password);

        var revoked = await here.PostAsync("/api/identity/account/me/sessions/revoke-others", null, Ct);

        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await here.GetAsync("/api/identity/account/me", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await elsewhere.GetAsync("/api/identity/account/me", Ct)).StatusCode);
    }

    [Fact]
    public async Task Sovereign_UnlocksAndResetsAMember()
    {
        var sovereignName = await CreateUserAsync(SecurityRoles.Sovereign);
        var memberName = await CreateUserAsync(SecurityRoles.Member);
        var memberId = await IdOfAsync(memberName);
        using var sovereign = await SignInAsync(sovereignName, Password);

        for (var attempt = 0; attempt < FailedAttemptsBeforeLockout; attempt++)
        {
            await LoginAsync(NewClient(), memberName, "wrong-pass-1");
        }

        Assert.Equal(HttpStatusCode.Locked, (await LoginAsync(NewClient(), memberName, Password)).StatusCode);

        var unlocked = await sovereign.PostAsync($"/api/identity/users/{memberId}/unlock", null, Ct);
        Assert.Equal(HttpStatusCode.NoContent, unlocked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(NewClient(), memberName, Password)).StatusCode);

        var reset = await sovereign.PutAsJsonAsync($"/api/identity/users/{memberId}/password", new ResetPasswordRequest(NewPassword), Ct);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(NewClient(), memberName, Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(NewClient(), memberName, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task ResetPassword_IsSovereignOnly_AndNotForYourself()
    {
        var sovereignName = await CreateUserAsync(SecurityRoles.Sovereign);
        var memberName = await CreateUserAsync(SecurityRoles.Member);
        using var sovereign = await SignInAsync(sovereignName, Password);
        using var member = await SignInAsync(memberName, Password);

        var byMember = await member.PutAsJsonAsync($"/api/identity/users/{await IdOfAsync(sovereignName)}/password", new ResetPasswordRequest(NewPassword), Ct);
        Assert.Equal(HttpStatusCode.Forbidden, byMember.StatusCode);

        var ofSelf = await sovereign.PutAsJsonAsync($"/api/identity/users/{await IdOfAsync(sovereignName)}/password", new ResetPasswordRequest(NewPassword), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, ofSelf.StatusCode);

        var weak = await sovereign.PutAsJsonAsync($"/api/identity/users/{await IdOfAsync(memberName)}/password", new ResetPasswordRequest("short"), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
    }

    [Fact]
    public async Task ResetPasswordCommand_PrintsAWorkingPassword()
    {
        var userName = await CreateUserAsync(SecurityRoles.Sovereign);
        await using var output = new StringWriter();

        var exitCode = await PasswordResetCommand.RunAsync(factory.Services, [PasswordResetCommand.Name, userName], output);

        Assert.Equal(0, exitCode);
        var printed = output.ToString().Split(": ", 2)[1].Split(Environment.NewLine)[0];
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(NewClient(), userName, printed)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(NewClient(), userName, Password)).StatusCode);
    }

    [Fact]
    public async Task ResetPasswordCommand_FailsForAnUnknownAccount()
    {
        await using var output = new StringWriter();

        var exitCode = await PasswordResetCommand.RunAsync(factory.Services, [PasswordResetCommand.Name, "nobody-here"], output);

        Assert.Equal(1, exitCode);
    }

    private async Task<string> CreateUserAsync(string role)
    {
        var userName = $"user-{Guid.NewGuid():N}"[..20];

        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = userName, FirstName = "Test" };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);

        return userName;
    }

    private async Task<Guid> IdOfAsync(string userName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByNameAsync(userName);
        return user!.Id;
    }

    private async Task<HttpClient> SignInAsync(string userName, string password)
    {
        var client = NewClient();
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, userName, password)).StatusCode);
        return client;
    }

    /// <summary>
    /// Over https, so the client sends back the Secure session cookie.
    /// </summary>
    private HttpClient NewClient()
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string userName, string password)
    {
        return client.PostAsJsonAsync("/api/identity/account/login", new LoginRequest(userName, password), Ct);
    }

    /// <summary>
    /// Real authentication, with the security stamp checked on every request so ended sessions fail at once.
    /// </summary>
    public sealed class Factory : MonolithWebApplicationFactory
    {
        protected override bool AuthenticateEveryRequest => false;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
                services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero));
        }
    }
}
