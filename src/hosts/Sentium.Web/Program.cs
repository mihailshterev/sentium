using FluentValidation;
using Microsoft.Extensions.Caching.Hybrid;
using Sentium.Core.Constants;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Mvc;
using Sentium.Infrastructure.Realtime;
using Sentium.Infrastructure.Validation;
using Sentium.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSentiumAuditLogging();

builder.AddSentiumObjectStore(ResourceNames.WorkspaceBlobs);

builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(30),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };
});

builder.AddSentiumPlatform();

builder.AddSentiumDataProtection();

// Sentium.Web only sends the X-Internal-Token (to the Sandbox and the Backup service); no endpoint here accepts it.
builder.AddInternalApiClient();

// Module registration order mirrors the dependency graph (dependencies first).
builder.AddSentiumModules();

builder.Services.AddOpenApi();
builder.Services.AddControllers(options => options.Filters.Add<FluentValidationFilter>())
    .AddSentiumModuleControllers()
    .AddInternalControllers();
builder.Services.AddValidatorsFromAssemblies(SentiumModules.Assemblies, includeInternalTypes: true);

var app = builder.Build();

app.UseSentiumTracing();
app.UseSentiumSecurityHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

await app.InitializeSentiumModulesAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseStaticFiles(PortalStaticFiles.Options);
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapControllers();
app.MapSentiumRealtime();

// Unknown API routes must 404 instead of falling through to the SPA shell.
app.MapFallback("api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html", PortalStaticFiles.Options);

app.Run();

namespace Sentium.Web
{
    public partial class Program;
}
