using AgentRuntime.Application;
using AgentRuntime.Infrastructure;
using AgentRuntime.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();

builder.AddNatsClient("nats");
builder.AddRedisDistributedCache("redis");
builder.Services.AddAgentRuntimeApplication();
builder.Services.AddAgentRuntimeInfrastructure(builder.Configuration);

builder.Services.AddHttpClient();

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
}

app.MapControllers();

app.Run();
