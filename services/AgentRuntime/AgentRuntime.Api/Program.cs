using AgentRuntime.Application;
using AgentRuntime.Infrastructure;
using AgentRuntime.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Scalar.AspNetCore;
using Sentium.Shared.Constants;

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

builder.Services.AddAgentRuntimeApplication();
builder.Services.AddAgentRuntimeInfrastructure(builder.Configuration);

builder.Services.AddHttpClient();

#pragma warning disable EXTEXP0001

builder.Services.AddHttpClient(ResourceNames.Ollama, client =>
{
    client.BaseAddress = new Uri("http://localhost:11434");
    client.Timeout = TimeSpan.FromMinutes(15);
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
builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(30),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };
});

var app = builder.Build();

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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
