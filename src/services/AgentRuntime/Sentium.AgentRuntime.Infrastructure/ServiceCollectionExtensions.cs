using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Conversations;
using Sentium.AgentRuntime.Core.Rag;
using Sentium.AgentRuntime.Core.Tools;
using Sentium.AgentRuntime.Core.WorkflowManagement;
using Sentium.AgentRuntime.Core.Workspaces;
using Sentium.AgentRuntime.Infrastructure.Agents;
using Sentium.AgentRuntime.Infrastructure.Conversations;
using Sentium.AgentRuntime.Infrastructure.Data;
using Sentium.AgentRuntime.Infrastructure.Rag;
using Sentium.AgentRuntime.Infrastructure.Tools;
using Sentium.AgentRuntime.Infrastructure.WorkflowManagement;
using Sentium.AgentRuntime.Infrastructure.WorkspaceManagement;
using Sentium.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;
using Sentium.Shared.Constants;
using Sentium.AgentRuntime.Core.Storage;
using Sentium.AgentRuntime.Infrastructure.Storage;

namespace Sentium.AgentRuntime.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentRuntimeInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

        services.AddDbContext<AgentRuntimeDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString(ResourceNames.AgentRuntimeDb))
        );

        var ollamaUri = new Uri(configuration["AI:OllamaBaseUrl"] ?? "http://localhost:11434");
        var modelName = configuration["AI:ModelName"] ?? AIModels.Gemma3_1B;

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

        services.AddSingleton<IEventBus, NatsEventBus>();

        services.AddSingleton<IEmbeddingService, OllamaEmbeddingService>();
        services.AddSingleton<IVectorRepository, QdrantVectorRepository>();
        services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();

        services.AddScoped<ILocalFileService, LocalFileService>();
        services.AddHostedService<FileIngestionWorker>();

        services.AddScoped<IAgentRegistry, AgentRegistry>();
        services.AddScoped<IAgentToolProvider, AgentToolProvider>();
        services.AddScoped<IAgentFactory, CompositeAgentFactory>();
        services.AddScoped<IAgentManager, AgentManager>();
        services.AddScoped<IWorkspaceManager, WorkspaceManager>();

        services.AddTransient<IAgentTool, KnowledgeBaseSearchTool>();
        services.AddTransient<IAgentTool, ReadFileTool>();
        services.AddTransient<IAgentTool, StoreMemoryTool>();
        services.AddTransient<IAgentTool, RecallMemoryTool>();

        services.AddScoped<IConversationManager, ConversationManager>();
        services.AddScoped<IWorkflowManager, WorkflowManager>();
        services.AddScoped<IWorkflowRunRepository, WorkflowRunRepository>();

        return services;
    }
}
