using Microsoft.Extensions.Hosting;

namespace Sentium.Infrastructure.Testing;

/// <summary>
/// The environment the e2e (Playwright) stack runs the host in: the database is dropped and seeded at startup.
/// </summary>
public static class TestingEnvironment
{
    public const string Name = "Testing";

    public static bool IsTesting(this IHostEnvironment environment) => environment.IsEnvironment(Name);
}
