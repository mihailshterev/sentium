using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Quartz;
using Sentium.Core.Constants;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Inference;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Agents.Native;
using Sentium.Praxis.Features.CodeExecution;
using Sentium.Praxis.Features.Conversations;
using Sentium.Praxis.Data;
using Sentium.Praxis.Extensions;
using Sentium.Praxis.Features.Learnings;
using Sentium.Praxis.Features.Models;
using Sentium.Praxis.Features.Orchestration;
using Sentium.Praxis.Features.Registry;
using Sentium.Praxis.Features.Scheduling;
using Sentium.Praxis.Features.Sentinel;
using Sentium.Praxis.Features.Skills;
using Sentium.Praxis.Features.Skills.BuiltIn;
using Sentium.Praxis.Features.Storage;
using Sentium.Praxis.Testing;
using Sentium.Praxis.Features.Tools;
using Sentium.Praxis.Features.Tools.Inventory;
using Sentium.Praxis.Features.Tools.Workspace;
using Sentium.Praxis.Features.WorkflowManagement;
using Sentium.Praxis.Features.WorkspaceManagement;
using Sentium.Praxis.Features.Workflows;
using Sentium.Praxis.Features.Workspaces;

namespace Sentium.Praxis;

public static class PraxisModuleExtensions
{
    internal const string ModuleName = "praxis";

    /// <summary>
    /// Named HttpClient for the agent chat models. Module-specific so it never merges configuration
    /// with the inference clients other modules register.
    /// </summary>
    internal const string LlmHttpClientName = "praxis-llm";

    public static IHostApplicationBuilder AddPraxisModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddModuleDbContext<PraxisDbContext>(PraxisDbContext.Schema);

        var services = builder.Services;
        var configuration = builder.Configuration;

        AddOrchestration(services);
        AddInference(services, configuration);

        services.AddSingleton<IRegistrySettingsService, RegistrySettingsService>();

        services.AddScoped<ILocalFileService, LocalFileService>();
        services.AddSingleton<FileIngestionQueue>();
        services.AddSingleton<IFileIngestionQueue>(sp => sp.GetRequiredService<FileIngestionQueue>());
        services.AddHostedService<FileIngestionWorker>();

        services.AddScoped<IAgentLearningRepository, AgentLearningRepository>();
        services.AddScoped<ILearningSanitizationPipeline, LearningSanitizationPipeline>();
        services.AddScoped<IAgentLearningService, AgentLearningService>();

        services.AddSingleton<IBuiltInSkillCatalog, BuiltInSkillCatalog>();
        services.AddScoped<IAgentSkillRepository, AgentSkillRepository>();
        services.AddScoped<IAgentSkillService, AgentSkillService>();
        services.AddScoped<DynamicSkillsProvider>();

        services.AddSingleton<IPendingApprovalStore, PendingApprovalStore>();

        services.AddScoped<IAgentRegistry, AgentRegistry>();
        services.AddScoped<IAgentToolProvider, AgentToolProvider>();
        services.AddScoped<IAgentFactory, CompositeAgentFactory>();
        services.AddScoped<IPromptEnhancementService, PromptEnhancementService>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();

        services.AddTransient<IAgentTool, KnowledgeBaseSearchTool>();
        services.AddTransient<IAgentTool, CodeExecutionSandboxTool>();
        services.AddTransient<IAgentTool, ReadFileTool>();
        services.AddTransient<IAgentTool, StoreMemoryTool>();
        services.AddTransient<IAgentTool, RecallMemoryTool>();
        services.AddTransient<IAgentTool, RecallLearningsTool>();
        services.AddTransient<IAgentTool, ListWorkspacesTool>();
        services.AddTransient<IAgentTool, ListWorkspaceFilesTool>();
        services.AddTransient<IAgentTool, ReadWorkspaceFileContentTool>();
        services.AddTransient<IAgentTool, WriteWorkspaceFileTool>();
        services.AddTransient<IAgentTool, CaptureAgentLearningTool>();
        services.AddTransient<IAgentTool, ScheduleTaskTool>();
        services.AddTransient<IAgentTool, FindAssetsTool>();
        services.AddTransient<IAgentTool, GetAssetTool>();
        services.AddTransient<IAgentTool, ListLocationContentsTool>();

        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IWorkflowRepository, WorkflowRepository>();
        services.AddScoped<IWorkflowRunRepository, WorkflowRunRepository>();

        AddScheduling(services);

        services.AddSingleton<SentinelClient>();
        services.AddSingleton<IPdpContextAccessor, PdpContextAccessor>();

        AddCodeExecution(services, configuration);

        services.AddHealthChecks().AddDbContextCheck<PraxisDbContext>("praxis-db", tags: [ModuleName]);

        return builder;
    }

    public static async Task InitializePraxisModuleAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        await app.Services.MigrateModuleDbContextAsync<PraxisDbContext>();

        if (app.Environment.IsEnvironment("Testing"))
        {
            await using var scope = app.Services.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();

            var db = scope.ServiceProvider.GetRequiredService<PraxisDbContext>();
            var testUserId = Guid.Parse(app.Configuration["E2E:UserId"] ?? "e2e00000-0000-0000-0000-000000000001");

            await E2EDataSeeder.SeedAsync(db, testUserId);
        }
    }

    private static void AddOrchestration(IServiceCollection services)
    {
        services.AddSingleton<IWorkflowExecutionRegistry, WorkflowExecutionRegistry>();
        services.AddSingleton<WorkflowRunQueue>();
        services.AddSingleton<IWorkflowRunQueue>(sp => sp.GetRequiredService<WorkflowRunQueue>());
        services.AddHostedService<WorkflowRunProcessor>();

        services.AddSingleton<StreamRelay>();
        services.AddSingleton<IStreamRelay>(sp => sp.GetRequiredService<StreamRelay>());
        services.AddSingleton<IAgentStreamPublisher>(sp => sp.GetRequiredService<StreamRelay>());
        services.AddHostedService(sp => sp.GetRequiredService<StreamRelay>());

        services.AddTransient<IAgentService, AgentService>();
        services.AddTransient<IConversationService, ConversationService>();
        services.AddTransient<IWorkflowService, WorkflowService>();
        services.AddTransient<IWorkspaceService, WorkspaceService>();
        services.AddTransient<IOrchestrator, WorkflowOrchestrator>();

        RegisterAgent<GeneralAssistant>(services, AgentRole.GeneralAssistant);
        RegisterAgent<OrchestratorAgent>(services, AgentRole.Orchestrator);
        RegisterAgent<SummaryAgent>(services, AgentRole.Summarizer);
        RegisterAgent<ValidationAgent>(services, AgentRole.Validator);
        RegisterAgent<ResearchAnalyst>(services, AgentRole.ResearchAnalyst);
        RegisterAgent<SoftwareEngineer>(services, AgentRole.SoftwareEngineer);
    }

    private static void RegisterAgent<T>(IServiceCollection services, string name) where T : class, IAgent
    {
        services.AddKeyedTransient<IAgent, T>(name);
        services.AddTransient<IAgent, T>();
    }

    private static void AddInference(IServiceCollection services, IConfiguration configuration)
    {
        services.AddInferenceHttpClient(configuration, LlmHttpClientName, InferenceEndpoint.Chat)
            .AddTypedClient<IModelCatalog, LlamaRouterModelCatalog>();

        // Default-model client for the non-agent passes (prompt enhancement, learning sanitization).
        services.AddChatClient(sp =>
        {
            var defaultModel = sp.GetRequiredService<IOptions<InferenceOptions>>().Value.DefaultModel;

            return sp.GetRequiredService<IInferenceClientFactory>()
                .CreateChatClient(LlmHttpClientName, defaultModel)
                .AddSentiumPipeline();
        });
    }

    private static void AddScheduling(IServiceCollection services)
    {
        services.AddQuartz(q =>
        {
            q.SchedulerName = SchedulerDefaults.SchedulerName;
            q.SchedulerId = "AUTO";

            q.MisfireThreshold = TimeSpan.FromMinutes(1);

            q.UsePersistentStore(store =>
            {
                store.UseProperties = true;
                store.PerformSchemaValidation = true;
                store.RetryInterval = TimeSpan.FromSeconds(15);

                store.UsePostgres(postgres =>
                {
                    postgres.ConnectionStringName = ResourceNames.SentiumDb;
                    postgres.TablePrefix = SchedulerDefaults.TablePrefix;
                });

                store.UseSystemTextJsonSerializer();
            });
        });

        services.AddQuartzHostedService(options =>
        {
            options.AwaitApplicationStarted = true;
            options.WaitForJobsToComplete = true;
        });
    }

    /// <summary>
    /// Code runs in the separate Sentium.Sandbox service (it owns the Docker socket); Praxis calls it over
    /// HTTP with the internal API key and keeps the execution history.
    /// </summary>
    private static void AddCodeExecution(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SandboxArtifactOptions>(configuration.GetSection(SandboxArtifactOptions.SectionName));
        services.AddScoped<IExecutionLogRepository, EfCoreExecutionLogRepository>();

        services.TryAddTransient<InternalApiKeyDelegatingHandler>();

        services.AddHttpClient<ISandboxClient, SandboxClient>(client =>
        {
            client.BaseAddress = new Uri($"https+http://{ServiceNames.Sandbox}");
            client.Timeout = Timeout.InfiniteTimeSpan;
        })
        .AddHttpMessageHandler<InternalApiKeyDelegatingHandler>()
        .AddLongRunningResilienceHandler(
            totalTimeout: TimeSpan.FromSeconds(130),
            attemptTimeout: TimeSpan.FromSeconds(120),
            retries: 0
        );
    }
}
