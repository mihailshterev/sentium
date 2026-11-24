using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentium.Vigil.Features.Monitoring;

namespace Sentium.Tests.Unit.Modules.Vigil;

/// <summary>
/// An <see cref="IncidentStore"/> whose database cannot be reached: every write stays queued and reads fall back to
/// the incidents kept in memory.
/// </summary>
internal static class OfflineIncidentStore
{
    public static IncidentStore Create() =>
        new(Substitute.For<IServiceScopeFactory>(), TimeProvider.System, NullLogger<IncidentStore>.Instance);
}
