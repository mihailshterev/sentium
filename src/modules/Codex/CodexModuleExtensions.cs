using Microsoft.Extensions.AI;
using OllamaSharp;
using Microsoft.EntityFrameworkCore;
using Sentium.Codex.Contracts;
using Sentium.Codex.Data;
using Sentium.Codex.Features.Rag;
using Sentium.Infrastructure.Extensions;

namespace Sentium.Codex;

/// <summary>
/// Codex owns the knowledge base: embeddings, the pgvector store (<c>codex</c> schema) and the ingestion pipeline.
/// Other modules own their content (learnings, memories, workspace files) and push it through the
/// <see cref="IDocumentIngestionService"/> / read it through <see cref="IVectorRepository"/>.
/// </summary>
public static class CodexModuleExtensions
{
    private const string OllamaHttpClientName = "codex-ollama";

    public static IHostApplicationBuilder AddCodexModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;
        var configuration = builder.Configuration;

        builder.AddModuleDbContext<CodexDbContext>(CodexDbContext.Schema, npgsql => npgsql.UseVector());

        services.Configure<RagOptions>(configuration.GetSection(RagOptions.SectionName));

        services.AddOllamaHttpClient(configuration, OllamaHttpClientName);

        services.AddEmbeddingGenerator(sp =>
        {
            var ragOptions = configuration.GetSection(RagOptions.SectionName).Get<RagOptions>() ?? new RagOptions();

            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(OllamaHttpClientName);
            IEmbeddingGenerator<string, Embedding<float>> generator = new OllamaApiClient(httpClient)
            {
                SelectedModel = ragOptions.EmbeddingModelName
            };

            return new EmbeddingGeneratorBuilder<string, Embedding<float>>(generator)
                .UseOpenTelemetry()
                .Build();
        });

        services.AddSingleton<IEmbeddingService, OllamaEmbeddingService>();
        services.AddScoped<IVectorRepository, PgVectorRepository>();
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
