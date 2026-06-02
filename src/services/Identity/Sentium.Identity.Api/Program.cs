using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Hybrid;
using Scalar.AspNetCore;
using Sentium.Identity.Application;
using Sentium.Identity.Infrastructure;
using Sentium.Infrastructure.Diagnostics;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Validation;
using Sentium.Shared.Constants;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddSentiumProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddControllers(options => options.Filters.Add<FluentValidationFilter>());
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.AddNatsClient(ResourceNames.Nats);
builder.AddRedisDistributedCache(ResourceNames.Redis);

builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(15),
        LocalCacheExpiration = TimeSpan.FromMinutes(3)
    };
});

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", policy =>
    {
        policy.PermitLimit = 10;
        policy.Window = TimeSpan.FromMinutes(1);
        policy.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        policy.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.AddSentiumAuditLogging();
builder.AddInfrastructure();
builder.Services.AddIdentityServices(builder.Configuration);
builder.Services.AddApplication();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseSentiumTracing();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Applying database migrations...");
    await app.ApplyMigrations();
    logger.LogInformation("Database migrations applied");

    app.MapOpenApi();

    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Sentium Identity")
                .WithTheme(ScalarTheme.DeepSpace)
                .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.MapDefaultEndpoints();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("index.html");

app.Run();

namespace Sentium.Identity.Api
{
    public partial class Program { }
}
