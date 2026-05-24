using Sentium.AgentRuntime.Application;
using Sentium.AgentRuntime.Infrastructure;
using Sentium.AgentRuntime.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Scalar.AspNetCore;
using Sentium.Shared.Constants;
using Sentium.Infrastructure.Extensions;
using Sentium.AgentRuntime.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddAuthenticationDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();

builder.AddNatsClient(ResourceNames.Nats);
builder.AddRedisDistributedCache(ResourceNames.Redis);
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

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.UseSentiumTracing();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Applying database migrations...");
    var db = scope.ServiceProvider.GetRequiredService<AgentRuntimeDbContext>();
    await db.Database.MigrateAsync();
    logger.LogInformation("Database migrations applied");

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
