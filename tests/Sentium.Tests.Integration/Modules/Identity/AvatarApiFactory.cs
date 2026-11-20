using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sentium.Infrastructure.Storage;
using Sentium.Tests.Integration.Common;

namespace Sentium.Tests.Integration.Modules.Identity;

public sealed class AvatarApiFactory : MonolithWebApplicationFactory
{
    public InMemoryObjectStore ObjectStore { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IObjectStore>();
            services.AddSingleton<IObjectStore>(ObjectStore);
        });
    }
}
