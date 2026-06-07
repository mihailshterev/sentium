using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Sentium.Infrastructure.Diagnostics;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Validation;
using Sentium.Sentinel.Application;
using Sentium.Sentinel.Infrastructure;
using Sentium.Sentinel.Infrastructure.Data;
using Sentium.Shared.Constants;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddAuthenticationDefaults();
builder.AddRoleAuthorization();

builder.Services.AddSentiumProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddControllers(options => options.Filters.Add<FluentValidationFilter>())
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.AddNatsClient(ResourceNames.Nats);
builder.AddRedisDistributedCache(ResourceNames.Redis);

builder.Services.AddHybridCache();

builder.AddSentiumAuditLogging();
builder.AddInfrastructure();
builder.Services.AddApplication(builder.Configuration);

var app = builder.Build();

app.UseSentiumTracing();
app.UseExceptionHandler();

if (app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Dropping and recreating sentineldb_e2e database...");
    var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
    await db.Database.EnsureDeletedAsync();
    await db.Database.MigrateAsync();
    logger.LogInformation("Seeding E2E sentinel audit records...");

    var now = DateTimeOffset.UtcNow;
    var entries = new[]
    {
        new AuditLogEntity { Id = Guid.NewGuid(), Timestamp = now.AddMinutes(-2), AgentId = "e2e-baseline-agent", SkillName = "web_search", ResourceType = "ExternalApi", ResourceId = "search-1", Action = "search", UserPromptHash = "abc123", CorrelationId = Guid.NewGuid().ToString(), Allowed = true, Effect = "Allow", Risk = "Low", Reason = "Within autonomy bounds", MetadataJson = "{}", TriggeredPoliciesJson = "[]" },
        new AuditLogEntity { Id = Guid.NewGuid(), Timestamp = now.AddMinutes(-1), AgentId = "e2e-baseline-agent", SkillName = "code_execution", ResourceType = "Sandbox", ResourceId = "job-1", Action = "execute", UserPromptHash = "def456", CorrelationId = Guid.NewGuid().ToString(), Allowed = true, Effect = "Allow", Risk = "Medium", Reason = "Code execution permitted", MetadataJson = "{}", TriggeredPoliciesJson = "[]" },
        new AuditLogEntity { Id = Guid.NewGuid(), Timestamp = now, AgentId = "e2e-baseline-agent", SkillName = "file_write", ResourceType = "File", ResourceId = "/tmp/test", Action = "write", UserPromptHash = "ghi789", CorrelationId = Guid.NewGuid().ToString(), Allowed = false, Effect = "Deny", Risk = "High", Reason = "File write denied by policy", MetadataJson = "{}", TriggeredPoliciesJson = "[\"HighRiskPolicy\"]" },
    };
    db.AuditLogs.AddRange(entries);
    await db.SaveChangesAsync();
    logger.LogInformation("Sentinel E2E seeding complete");
}
else if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Applying Sentinel database migrations...");
    var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
    await db.Database.MigrateAsync();
    logger.LogInformation("Sentinel database migrations applied");
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Sentium Sentinel")
                .WithTheme(ScalarTheme.DeepSpace)
                .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

namespace Sentium.Sentinel.Api
{
    public partial class Program { }
}

