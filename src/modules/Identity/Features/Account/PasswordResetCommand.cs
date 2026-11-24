using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Sentium.Identity.Entities;
using Sentium.Identity.Features.Users;
using Sentium.Infrastructure.Security;

namespace Sentium.Identity.Features.Account;

/// <summary>
/// <c>reset-password &lt;username&gt;</c>: the way back in when the only Sovereign forgot their password. It needs a
/// shell on the host (<c>docker compose exec sentium-web dotnet Sentium.Web.dll reset-password alice</c>), which is the
/// authority here. It sets a generated password, clears any lockout, ends the account's sessions and prints the
/// password once.
/// </summary>
internal static class PasswordResetCommand
{
    public const string Name = "reset-password";

    private const string Alphabet = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int GeneratedPasswordLength = 20;

    public static bool IsCommand(string[] args) => args.Length > 0 && string.Equals(args[0], Name, StringComparison.Ordinal);

    public static async Task<int> RunAsync(IServiceProvider services, string[] args, TextWriter output)
    {
        if (args.Length != 2 || string.IsNullOrWhiteSpace(args[1]))
        {
            await output.WriteLineAsync($"Usage: {Name} <username>");
            return 2;
        }

        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();

        var userName = args[1].Trim();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByNameAsync(userName);
        if (user is null)
        {
            await output.WriteLineAsync($"There is no account named '{userName}'.");
            return 1;
        }

        var password = GeneratePassword();
        var (succeeded, errors) = await scope.ServiceProvider.GetRequiredService<IUserService>()
            .ResetPasswordAsync(user.Id, password, CancellationToken.None);

        if (!succeeded)
        {
            await output.WriteLineAsync($"The password was not reset: {string.Join(" ", errors)}");
            return 1;
        }

        await output.WriteLineAsync($"The password of '{user.UserName}' is now: {password}");
        await output.WriteLineAsync("Sign in with it, then change it on the Profile page.");
        return 0;
    }

    /// <summary>
    /// Random over an alphabet without look-alike characters, redrawn until it has the digit and lowercase letter the
    /// password rules require.
    /// </summary>
    private static string GeneratePassword()
    {
        while (true)
        {
            var candidate = RandomNumberGenerator.GetString(Alphabet, GeneratedPasswordLength);
            if (candidate.Any(char.IsDigit) && candidate.Any(char.IsLower))
            {
                return candidate;
            }
        }
    }
}
