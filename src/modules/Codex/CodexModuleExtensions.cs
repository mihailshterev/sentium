using Microsoft.Extensions.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sentium.Codex.Contracts;
using Sentium.Codex.Data;
using Sentium.Codex.Features.Rag;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Inference;

namespace Sentium.Codex;

/// <summary>
/// Codex owns the knowledge base: embeddings, the pgvector store (<c>codex</c> schema) and the ingestion pipeline.
/// Other modules own their content (learnings, memories, workspace files) and push it through the
/// <see cref="IDocumentIngestionService"/> / read it through <see cref="IVectorRepository"/>.
/// </summary>
public static class CodexModuleExtensions
{
    /// <summary>
    /// Named HttpClient for the embedding server. Module-specific so it never merges configuration
    /// with the inference clients other modules register.
    /// </summary>
    private const string EmbeddingsHttpClientName = "codex-embeddings";

    public static IHostApplicationBuilder AddCodexModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;
        var configuration = builder.Configuration;

        builder.AddModuleDbContext<CodexDbContext>(CodexDbContext.Schema, npgsql => npgsql.UseVector());

        services.Configure<RagOptions>(configuration.GetSection(RagOptions.SectionName));

        services.AddInferenceHttpClient(configuration, EmbeddingsHttpClientName, InferenceEndpoint.Embedding);

        services.AddEmbeddingGenerator(sp =>
        {
            var ragOptions = sp.GetRequiredService<IOptions<RagOptions>>().Value;

            var generator = sp.GetRequiredService<IInferenceClientFactory>()
                .CreateEmbeddingGenerator(EmbeddingsHttpClientName, ragOptions.EmbeddingModelName);

            return new EmbeddingGeneratorBuilder<string, Embedding<float>>(generator)
                .UseOpenTelemetry()
                .Build();
        });

        services.AddSingleton<IEmbeddingService, EmbeddingService>();
        services.AddScoped<IVectorRepository, PgVectorRepository>();
        services.AddScoped<EmbeddingReindexer>();
        services.AddHostedService<EmbeddingReindexService>();
        services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();

        services.AddHealthChecks().AddDbContextCheck<CodexDbContext>("codex-db", tags: [ModuleName]);

        return builder;
    }

    public static Task InitializeCodexModuleAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Services.MigrateModuleDbContextAsync<CodexDbContext>();
    }

    internal const string ModuleName = "codex";
}
