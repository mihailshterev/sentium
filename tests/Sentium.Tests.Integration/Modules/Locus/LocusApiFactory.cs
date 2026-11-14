using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Sentium.Codex.Contracts;
using Sentium.Locus.Features.Attachments;
using Sentium.Tests.Integration.Common;

namespace Sentium.Tests.Integration.Modules.Locus;

/// <summary>
/// The monolith with Locus' outside dependencies swapped out: blob storage becomes an in-memory store and the
/// knowledge base a substitute (so sync tests can assert exactly what would have been written).
/// </summary>
public sealed class LocusApiFactory : MonolithWebApplicationFactory
{
    public InMemoryAttachmentStore AttachmentStore { get; } = new();

    public IDocumentIngestionService Ingestion { get; } = Substitute.For<IDocumentIngestionService>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAttachmentStore>();
            services.AddSingleton<IAttachmentStore>(AttachmentStore);

            services.RemoveAll<IDocumentIngestionService>();
            services.AddSingleton(Ingestion);
        });
    }
}
