using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Conversations;
using AgentRuntime.Core.Rag;
using AgentRuntime.Core.Tools;
using AgentRuntime.Core.WorkflowManagement;
using AgentRuntime.Infrastructure.Agents;
using AgentRuntime.Infrastructure.Conversations;
using AgentRuntime.Infrastructure.Data;
using AgentRuntime.Infrastructure.Rag;
using AgentRuntime.Infrastructure.Tools;
using AgentRuntime.Infrastructure.WorkflowManagement;
using Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;

namespace AgentRuntime.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentRuntimeInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

        services.AddDbContext<AgentRuntimeDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("agentruntimedb"))
        );

        var ollamaUri = new Uri(configuration["AI:OllamaBaseUrl"] ?? "http://localhost:11434");
        var modelName = configuration["AI:ModelName"] ?? "gemma3:1b";

        services.AddChatClient(sp =>
        {
            var client = new OllamaApiClient(ollamaUri, modelName);
            return new ChatClientBuilder(client)
                .UseFunctionInvocation()
                .UseOpenTelemetry()
                .Build();
        });

        services.Configure<RagOptions>(configuration.GetSection(RagOptions.SectionName));

        services.AddEmbeddingGenerator(sp =>
        {
            var ragOptions = configuration.GetSection(RagOptions.SectionName).Get<RagOptions>() ?? new RagOptions();
            IEmbeddingGenerator<string, Embedding<float>> generator = new OllamaApiClient(ollamaUri, ragOptions.EmbeddingModelName);

            return new EmbeddingGeneratorBuilder<string, Embedding<float>>(generator)
                .UseOpenTelemetry()
                .Build();
        });

        services.AddSingleton<IEmbeddingService, OllamaEmbeddingService>();
        services.AddSingleton<IVectorRepository, QdrantVectorRepository>();
        services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();

        services.AddTransient<IAgentTool, KnowledgeBaseSearchTool>();
        services.AddTransient<IAgentTool, ReadFileTool>();

        services.AddSingleton<IAgentRegistry, AgentRegistry>();
        services.AddSingleton<IAgentToolProvider, AgentToolProvider>();
        services.AddSingleton<IEventBus, NatsEventBus>();

        services.AddScoped<IAgentFactory, CompositeAgentFactory>();
        services.AddScoped<IAgentManager, AgentManager>();
        services.AddScoped<IConversationManager, ConversationManager>();
        services.AddScoped<IWorkflowManager, WorkflowManager>();
        services.AddScoped<IWorkflowRunRepository, WorkflowRunRepository>();

        return services;
    }
}
