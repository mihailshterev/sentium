using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Sentium.Infrastructure.Diagnostics;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Validation;
using Sentium.Sandbox.Application;
using Sentium.Sandbox.Infrastructure;
using Sentium.Sandbox.Infrastructure.Data;
using Sentium.Shared.Constants;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddAuthenticationDefaults();

builder.Services.AddSentiumProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddControllers(options => options.Filters.Add<FluentValidationFilter>());
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.AddNatsClient(ResourceNames.Nats);

builder.AddSandboxInfrastructure();
builder.Services.AddSandboxApplication(builder.Configuration);

var app = builder.Build();

app.UseSentiumTracing();
app.UseExceptionHandler();

if (app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<SandboxDbContext>();

    logger.LogInformation("Dropping and recreating sandboxdb_e2e database...");
    await db.Database.EnsureDeletedAsync();
    await db.Database.MigrateAsync();
    logger.LogInformation("Sandbox database ready");
}
else if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Applying Sandbox database migrations...");
    var db = scope.ServiceProvider.GetRequiredService<SandboxDbContext>();
    await db.Database.MigrateAsync();
    logger.LogInformation("Sandbox database migrations applied");
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Sentium Sandbox")
               .WithTheme(ScalarTheme.DeepSpace)
               .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

namespace Sentium.Sandbox.Api
{
    public partial class Program { }
}
