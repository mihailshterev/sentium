using FluentValidation;
using Scalar.AspNetCore;
using Sentium.Infrastructure.Diagnostics;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Validation;
using Sentium.Registry.Application;
using Sentium.Registry.Infrastructure;
using Sentium.Registry.Infrastructure.Data;
using Sentium.Shared.Constants;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddAuthenticationDefaults();
builder.AddInternalApiSecurity();
builder.AddRoleAuthorization();

builder.Services.AddSentiumProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddControllers(options => options.Filters.Add<FluentValidationFilter>());
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.AddNatsClient(ResourceNames.Nats);
builder.AddSentiumDistributedCache(ResourceNames.Redis);

builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new()
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };
});

builder.AddSentiumAuditLogging();
builder.AddRegistryInfrastructure();
builder.Services.AddRegistryApplication();

var app = builder.Build();

app.UseSentiumTracing();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RegistryDbContext>();
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Dropping and recreating registrydb_e2e database...");
    await db.Database.EnsureDeletedAsync();
}

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Applying Registry database migrations...");
    await app.ApplyMigrations();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
        options.WithTitle("Sentium Registry")
               .WithTheme(ScalarTheme.DeepSpace)
               .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient));
}

app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

namespace Sentium.Registry.Api
{
    public partial class Program { }
}
