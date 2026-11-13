namespace Sentium.Infrastructure.Testing;

/// <summary>
/// Shared inputs for every <see cref="IE2ESeeder"/>.
/// </summary>
public sealed record E2ESeedContext(Guid UserId, TimeProvider TimeProvider);
