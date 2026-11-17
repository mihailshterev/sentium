using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Sentium.Infrastructure.Storage;
using Sentium.Praxis.Features.Storage;
using Sentium.Tests.Integration.Common;

namespace Sentium.Tests.Integration.Modules.Praxis;

public sealed class WorkspaceApiFactory : MonolithWebApplicationFactory
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ILocalFileService>();
            services.RemoveAll<IObjectStore>();
            services.AddSingleton(Substitute.For<ILocalFileService>());
            services.AddSingleton(Substitute.For<IObjectStore>());
        });
    }
}
