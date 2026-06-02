using Sentium.Shared.Constants;

namespace Sentium.Tests.Integration.Common;

public sealed class SentinelTestFactory : ServiceWebApplicationFactory<Sentium.Sentinel.Api.Program>
{
    public SentinelTestFactory() : base(ResourceNames.SentinelDb, withRedis: false) { }
}
