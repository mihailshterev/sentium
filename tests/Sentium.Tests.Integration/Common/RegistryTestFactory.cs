using Sentium.Shared.Constants;

namespace Sentium.Tests.Integration.Common;

public sealed class RegistryTestFactory : ServiceWebApplicationFactory<Sentium.Registry.Api.Program>
{
    public RegistryTestFactory() : base(ResourceNames.RegistryDb, withRedis: true) { }
}
