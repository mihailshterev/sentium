using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sentium.Sentinel.Core.Settings;
using Sentium.Shared.Constants;

namespace Sentium.Tests.Integration.Common;

public sealed class SentinelTestFactory : ServiceWebApplicationFactory<Sentium.Sentinel.Api.Program>
{
    public SentinelTestFactory() : base(ResourceNames.SentinelDb, withRedis: false) { }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPdpRuntimeSettingsProvider>();
            services.AddSingleton<IPdpRuntimeSettingsProvider>(new InMemoryPdpSettingsProvider());
        });
    }

    private sealed class InMemoryPdpSettingsProvider : IPdpRuntimeSettingsProvider
    {
        private PdpRuntimeSettings _current = new();

        public ValueTask<PdpRuntimeSettings> GetAsync(CancellationToken ct = default) =>
            ValueTask.FromResult(_current);

        public Task UpdateAsync(PdpRuntimeSettings settings, CancellationToken ct = default)
        {
            _current = settings;
            return Task.CompletedTask;
        }
    }
}
