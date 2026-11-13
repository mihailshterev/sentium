namespace Sentium.Infrastructure.Testing;

/// <summary>
/// Seeds baseline data for the Playwright suite. Implementations live outside the module assemblies (Sentium.E2ESeed)
/// and are only registered and run when the host starts in the Testing environment.
/// </summary>
public interface IE2ESeeder
{
    Task SeedAsync(IServiceProvider services, E2ESeedContext context, CancellationToken ct = default);
}

/// <summary>Shared inputs for every <see cref="IE2ESeeder"/>.</summary>
public sealed record E2ESeedContext(Guid UserId, TimeProvider TimeProvider);
