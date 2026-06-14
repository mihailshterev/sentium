using Sentium.AgentRuntime.Application;
using Sentium.AgentRuntime.Infrastructure;
using Sentium.AgentRuntime.Infrastructure.Data;
using Sentium.AgentRuntime.Infrastructure.Testing;
using Sentium.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Scalar.AspNetCore;
using Sentium.Shared.Constants;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Caching;
using Sentium.Infrastructure.Diagnostics;
using Sentium.Infrastructure.Validation;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddAuthenticationDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers(options => options.Filters.Add<FluentValidationFilter>());
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.AddNatsClient(ResourceNames.Nats);
builder.AddSentiumDistributedCache(ResourceNames.Redis);
builder.AddQdrantClient(ResourceNames.Qdrant);

builder.AddAzureBlobServiceClient(ResourceNames.WorkspaceBlobs);

builder.Services.AddAgentRuntimeApplication();
builder.AddSentiumAuditLogging();
builder.AddAgentRuntimeInfrastructure();

builder.Services.AddHttpClient();

builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(30),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };
});
builder.Services.AddScoped<IScopedCache, ScopedCache>();

builder.Services.AddSentiumProblemDetails();

var app = builder.Build();

app.UseSentiumTracing();

// Configure the HTTP request pipeline.
if (app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
    var db = scope.ServiceProvider.GetRequiredService<AgentRuntimeDbContext>();

    logger.LogInformation("Dropping and recreating agentRuntime_e2e database...");
    await db.Database.EnsureDeletedAsync();
    await db.Database.MigrateAsync();
    logger.LogInformation("Database ready. Seeding E2E baseline data...");

    var testUserId = Guid.Parse(app.Configuration["E2E:UserId"] ?? "e2e00000-0000-0000-0000-000000000001");
    await E2EDataSeeder.SeedAsync(db, testUserId);
    logger.LogInformation("E2E seeding complete");
}
else if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Applying database migrations...");
    var db = scope.ServiceProvider.GetRequiredService<AgentRuntimeDbContext>();
    await db.Database.MigrateAsync();
    logger.LogInformation("Database migrations applied");
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Sentium Agent Runtime")
               .WithTheme(ScalarTheme.DeepSpace)
               .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.UseExceptionHandler();

app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

namespace Sentium.AgentRuntime.Api
{
    public partial class Program { }
}
