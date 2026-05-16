using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Conversations;
using Sentium.AgentRuntime.Core.Learnings;
using Sentium.AgentRuntime.Core.Rag;
using Sentium.AgentRuntime.Core.Settings;
using Sentium.AgentRuntime.Core.Skills;
using Sentium.AgentRuntime.Core.Tools;
using Sentium.AgentRuntime.Core.WorkflowManagement;
using Sentium.AgentRuntime.Core.Workspaces;
using Sentium.AgentRuntime.Infrastructure.Agents;
using Sentium.AgentRuntime.Infrastructure.Conversations;
using Sentium.AgentRuntime.Infrastructure.Data;
using Sentium.AgentRuntime.Infrastructure.Learnings;
using Sentium.AgentRuntime.Infrastructure.Rag;
using Sentium.AgentRuntime.Infrastructure.Settings;
using Sentium.AgentRuntime.Infrastructure.Sentinel;
using Sentium.AgentRuntime.Infrastructure.Skills;
using Sentium.AgentRuntime.Infrastructure.Skills.BuiltIn;
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
using Sentium.AgentRuntime.Infrastructure.Extensions;

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
        var modelName = configuration["AI:ModelName"] ?? AIModels.Gemma4;

        services.AddSingleton(new OllamaOptions { BaseUrl = ollamaUri, DefaultModel = modelName });

#pragma warning disable EXTEXP0001
        services.AddHttpClient(ResourceNames.Ollama, client =>
        {
            client.BaseAddress = ollamaUri;
            client.Timeout = TimeSpan.FromMinutes(10);
        })
        .RemoveAllResilienceHandlers()
        .AddStandardResilienceHandler(options =>
        {
            options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(10);
            options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(3);
            options.Retry.MaxRetryAttempts = 1;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(11);
        });
#pragma warning restore EXTEXP0001

        services.AddChatClient(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(ResourceNames.Ollama);
            var client = new OllamaApiClient(httpClient)
            {
                SelectedModel = modelName
            };

            return client.AddSentiumPipeline();
        });

        services.Configure<RagOptions>(configuration.GetSection(RagOptions.SectionName));

        services.AddEmbeddingGenerator(sp =>
        {
            var ragOptions = configuration.GetSection(RagOptions.SectionName).Get<RagOptions>() ?? new RagOptions();

            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(ResourceNames.Ollama);
            IEmbeddingGenerator<string, Embedding<float>> generator = new OllamaApiClient(httpClient)
            {
                SelectedModel = ragOptions.EmbeddingModelName
            };

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

        services.AddScoped<ISystemSettingsRepository, SystemSettingsRepository>();
        services.AddScoped<ISystemSettingsService, SystemSettingsService>();
        services.AddScoped<IAgentLearningRepository, AgentLearningRepository>();
        services.AddScoped<IAgentLearningService, AgentLearningService>();

        services.AddSingleton<IBuiltInSkillCatalog, BuiltInSkillCatalog>();
        services.AddScoped<IAgentSkillRepository, AgentSkillRepository>();
        services.AddScoped<IAgentSkillService, AgentSkillService>();
        services.AddScoped<DynamicSkillsProvider>();

        services.AddSingleton<IPendingApprovalStore, PendingApprovalStore>();

        services.AddScoped<IAgentRegistry, AgentRegistry>();
        services.AddScoped<IAgentToolProvider, AgentToolProvider>();
        services.AddScoped<IAgentFactory, CompositeAgentFactory>();
        services.AddScoped<IAgentManager, AgentManager>();
        services.AddScoped<IWorkspaceManager, WorkspaceManager>();

        services.AddTransient<IAgentTool, KnowledgeBaseSearchTool>();
        services.AddTransient<IAgentTool, CodeExecutionSandboxTool>();
        services.AddTransient<IAgentTool, ReadFileTool>();
        services.AddTransient<IAgentTool, StoreMemoryTool>();
        services.AddTransient<IAgentTool, RecallMemoryTool>();
        services.AddTransient<IAgentTool, ListWorkspacesTool>();
        services.AddTransient<IAgentTool, ListWorkspaceFilesTool>();
        services.AddTransient<IAgentTool, ReadWorkspaceFileContentTool>();
        services.AddTransient<IAgentTool, WriteWorkspaceFileTool>();
        services.AddTransient<IAgentTool, CaptureAgentLearningTool>();

        services.AddScoped<IConversationManager, ConversationManager>();
        services.AddScoped<IWorkflowManager, WorkflowManager>();
        services.AddScoped<IWorkflowRunRepository, WorkflowRunRepository>();

        services.AddHttpClient<SentinelClient>(client =>
        {
            client.BaseAddress = new Uri($"https+http://{ServiceNames.Sentinel}");
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddHttpClient("SandboxService", client =>
        {
            client.BaseAddress = new Uri($"https+http://{ServiceNames.Sandbox}");
            client.Timeout = TimeSpan.FromSeconds(120);
        }).AddStandardResilienceHandler();

        services.AddScoped<IPdpContextAccessor, PdpContextAccessor>();

        return builder;
    }
}
