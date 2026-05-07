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
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;
using Sentium.Shared.Constants;
using Sentium.AgentRuntime.Infrastructure.Tools.Workspace;
using Sentium.AgentRuntime.Core.Storage;
using Sentium.AgentRuntime.Infrastructure.Storage;
using Microsoft.Extensions.Hosting;
using Sentium.Infrastructure.Extensions;

namespace Sentium.AgentRuntime.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IHostApplicationBuilder AddAgentRuntimeInfrastructure(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        builder.AddAuditedDbContext<AgentRuntimeDbContext>(ResourceNames.AgentRuntimeDb);

        var services = builder.Services;
        var configuration = builder.Configuration;

        var ollamaUri = new Uri(configuration["AI:OllamaBaseUrl"] ?? "http://localhost:11434");
        var modelName = configuration["AI:ModelName"] ?? AIModels.Gemma3_1B;

        services.AddSingleton(new OllamaOptions { BaseUrl = ollamaUri, DefaultModel = modelName });

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
        services.AddTransient<IAgentTool, ListWorkspacesTool>();
        services.AddTransient<IAgentTool, ListWorkspaceFilesTool>();
        services.AddTransient<IAgentTool, ReadWorkspaceFileContentTool>();
        services.AddTransient<IAgentTool, WriteWorkspaceFileTool>();

        services.AddScoped<IConversationManager, ConversationManager>();
        services.AddScoped<IWorkflowManager, WorkflowManager>();
        services.AddScoped<IWorkflowRunRepository, WorkflowRunRepository>();

        return builder;
    }
}
