using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Inference;
using Sentium.Sentinel.Features.Audit;
using Sentium.Sentinel.Contracts;
using Sentium.Sentinel.Data;
using Sentium.Sentinel.Features.Engine;
using Sentium.Sentinel.Features.Options;
using Sentium.Sentinel.Features.Policies;
using Sentium.Sentinel.Features.RateLimiting;
using Sentium.Sentinel.Features.Settings;

namespace Sentium.Sentinel;

public static class SentinelModuleExtensions
{
    internal const string ModuleName = "sentinel";

    /// <summary>
    /// DI key of Sentinel's raw (un-harnessed) chat client used for semantic intent checks.
    /// Keyed so it never collides with the agent chat pipeline Praxis registers.
    /// </summary>
    public const string ChatClientKey = "sentinel";

    /// <summary>
    /// Named HttpClient behind <see cref="ChatClientKey"/>. Module-specific so it never merges
    /// configuration with the agent clients Praxis registers.
    /// </summary>
    private const string LlmHttpClientName = "sentinel-llm";

    public static IHostApplicationBuilder AddSentinelModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;
        var configuration = builder.Configuration;

        services.Configure<PdpOptions>(configuration.GetSection(PdpOptions.SectionName));

        services.AddScoped<SentinelPolicyEngine>();
        services.AddSingleton<IPolicyDecisionPoint, PolicyDecisionPoint>();

        services.AddSingleton<IRateLimitStore, InMemoryRateLimitStore>();

        // Defense-in-depth policy chain. Order matters: the engine evaluates in registration
        // order and short-circuits on the first deny. Cheap, deterministic, hard-deny policies
        // run first; the expensive LLM-based SemanticIntentPolicy runs last. Lockdown is first
        // so the kill-switch halts everything immediately.
        services.AddSingleton<IPdpPolicy, LockdownPolicy>();
        services.AddSingleton<IPdpPolicy, ProtectedResourcePolicy>();
        services.AddSingleton<IPdpPolicy, SensitiveDataEgressPolicy>();
        services.AddSingleton<IPdpPolicy, RateLimitingPolicy>();
        services.AddSingleton<IPdpPolicy, SemanticIntentPolicy>();

        builder.AddModuleDbContext<SentinelDbContext>(SentinelDbContext.Schema);
        services.AddScoped<IAuditLog, EfCoreAuditLog>();

        // Intent checks are short classifications bounded by PdpOptions.IntentCheckTimeoutSeconds, so this
        // client gets far tighter limits than the agent clients.
        services.AddInferenceHttpClient(
            configuration,
            LlmHttpClientName,
            InferenceEndpoint.Chat,
            totalTimeout: TimeSpan.FromMinutes(5),
            attemptTimeout: TimeSpan.FromMinutes(2));

        // Bound to the default chat model; SemanticIntentPolicy overrides it per request with the
        // Registry's IntentCheckModel through ChatOptions.ModelId.
        services.AddKeyedChatClient(ChatClientKey, sp => sp.GetRequiredService<IInferenceClientFactory>()
            .CreateChatClient(LlmHttpClientName, sp.GetRequiredService<IOptions<InferenceOptions>>().Value.DefaultModel))
            .UseOpenTelemetry();

        services.AddSingleton<IPdpRuntimeSettingsProvider, RegistryPdpSettingsProvider>();

        services.AddHealthChecks().AddDbContextCheck<SentinelDbContext>("sentinel-db", tags: [ModuleName]);

        return builder;
    }

    public static async Task InitializeSentinelModuleAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        await app.Services.MigrateModuleDbContextAsync<SentinelDbContext>();

        if (app.Environment.IsEnvironment("Testing"))
        {
            await SeedE2EAuditRecordsAsync(app.Services);
        }
    }

    private static async Task SeedE2EAuditRecordsAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();

        var now = DateTimeOffset.UtcNow;
        var entries = new[]
        {
            new AuditLogEntity { Id = Guid.NewGuid(), Timestamp = now.AddMinutes(-2), AgentId = "e2e-baseline-agent", SkillName = "web_search", ResourceType = "ExternalApi", ResourceId = "search-1", Action = "search", UserPromptHash = "abc123", CorrelationId = Guid.NewGuid().ToString(), Allowed = true, Effect = "Allow", Risk = "Low", Reason = "Within autonomy bounds", MetadataJson = "{}", TriggeredPoliciesJson = "[]" },
            new AuditLogEntity { Id = Guid.NewGuid(), Timestamp = now.AddMinutes(-1), AgentId = "e2e-baseline-agent", SkillName = "code_execution", ResourceType = "Sandbox", ResourceId = "job-1", Action = "execute", UserPromptHash = "def456", CorrelationId = Guid.NewGuid().ToString(), Allowed = true, Effect = "Allow", Risk = "Medium", Reason = "Code execution permitted", MetadataJson = "{}", TriggeredPoliciesJson = "[]" },
            new AuditLogEntity { Id = Guid.NewGuid(), Timestamp = now, AgentId = "e2e-baseline-agent", SkillName = "file_write", ResourceType = "File", ResourceId = "/tmp/test", Action = "write", UserPromptHash = "ghi789", CorrelationId = Guid.NewGuid().ToString(), Allowed = false, Effect = "Deny", Risk = "High", Reason = "File write denied by policy", MetadataJson = "{}", TriggeredPoliciesJson = "[\"HighRiskPolicy\"]" },
        };

        db.AuditLogs.AddRange(entries);
        await db.SaveChangesAsync();
    }
}
