using Microsoft.Extensions.AI;
using OllamaSharp;
using Sentium.Codex.Contracts;
using Sentium.Codex.Features.Rag;
using Sentium.Core.Constants;
using Sentium.Infrastructure.Extensions;

namespace Sentium.Codex;

/// <summary>
/// Codex owns the knowledge base: embeddings, the Qdrant vector store and the ingestion pipeline.
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

        builder.AddQdrantClient(ResourceNames.Qdrant);

        services.Configure<RagOptions>(configuration.GetSection(RagOptions.SectionName));

        var ollamaUri = new Uri(configuration["AI:OllamaBaseUrl"] ?? "http://localhost:11434");
        var ollamaTotalTimeout = ResolveTimeout(configuration, "AI:OllamaTimeout", TimeSpan.FromMinutes(30));
        var ollamaAttemptTimeout = ResolveTimeout(configuration, "AI:OllamaAttemptTimeout", TimeSpan.FromMinutes(10));

        services.AddHttpClient(OllamaHttpClientName, client =>
        {
            client.BaseAddress = ollamaUri;
            client.Timeout = ollamaTotalTimeout;
        })
        .AddLongRunningResilienceHandler(
            totalTimeout: ollamaTotalTimeout,
            attemptTimeout: ollamaAttemptTimeout,
            retries: 0
        );

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
        services.AddSingleton<IVectorRepository, QdrantVectorRepository>();
        services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();

        return builder;
    }

    private static TimeSpan ResolveTimeout(IConfiguration configuration, string key, TimeSpan fallback)
    {
        var raw = configuration[key];
        if (!string.IsNullOrWhiteSpace(raw))
        {
            if (TimeSpan.TryParse(raw, out var asTimeSpan) && asTimeSpan > TimeSpan.Zero)
            {
                return asTimeSpan;
            }

            if (double.TryParse(raw, out var asMinutes) && asMinutes > 0)
            {
                return TimeSpan.FromMinutes(asMinutes);
            }
        }

        return fallback;
    }
}
