using FluentValidation;
using Microsoft.Extensions.Caching.Hybrid;
using Scalar.AspNetCore;
using Sentium.Core.Constants;
using Sentium.Infrastructure.Diagnostics;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Validation;
using Sentium.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSentiumAuditLogging();

builder.AddNatsClient(ResourceNames.Nats);
builder.AddSentiumDistributedCache(ResourceNames.Redis);
builder.AddAzureBlobServiceClient(ResourceNames.WorkspaceBlobs);

builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(30),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };
});

builder.AddSentiumPlatform();

// X-Internal-Token scheme for the out-of-process Sandbox service (Sentinel's evaluate endpoint, Praxis' sandbox client).
builder.AddInternalApiSecurity();

// Module registration order mirrors the dependency graph (dependencies first).
builder.AddSentiumModules();

builder.Services.AddOpenApi();
builder.Services.AddControllers(options => options.Filters.Add<FluentValidationFilter>())
    .AddSentiumModuleControllers();
builder.Services.AddValidatorsFromAssemblies(SentiumModules.Assemblies);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173", "https://localhost:5173"];
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseSentiumTracing();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    await app.InitializeSentiumModulesAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Sentium")
               .WithTheme(ScalarTheme.DeepSpace)
               .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.UseStaticFiles();
app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapControllers();
app.MapSentiumModuleEndpoints();

// Unknown API routes must 404 instead of falling through to the SPA shell.
app.MapFallback("api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();

namespace Sentium.Web
{
    public partial class Program;
}
