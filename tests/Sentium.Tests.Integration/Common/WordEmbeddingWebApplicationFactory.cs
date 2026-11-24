using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sentium.Codex.Contracts;

namespace Sentium.Tests.Integration.Common;

/// <summary>
/// The monolith with <see cref="WordEmbeddingService"/> in place of the embedding server.
/// </summary>
public sealed class WordEmbeddingWebApplicationFactory : MonolithWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<IEmbeddingService, WordEmbeddingService>()));
    }
}
