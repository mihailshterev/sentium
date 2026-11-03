using Docker.DotNet;
using FluentValidation;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Sentium.Core.Constants;
using Sentium.Infrastructure.Diagnostics;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Security;
using Sentium.Infrastructure.Validation;
using Sentium.Sandbox.Artifacts;
using Sentium.Sandbox.Docker;
using Sentium.Sandbox.Execution;
using Sentium.Sandbox.Options;
using Sentium.Sandbox.Sentinel;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSentiumAuditLogging();
builder.AddInternalApiSecurity();

builder.Services.AddSentiumProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddControllers(options => options.Filters.Add<FluentValidationFilter>());
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.Configure<SandboxOptions>(builder.Configuration.GetSection(SandboxOptions.SectionName));

builder.AddAzureBlobServiceClient(ResourceNames.WorkspaceBlobs);
builder.Services.AddSingleton<IArtifactService, ArtifactService>();

builder.Services.AddSingleton<IDockerClient>(sp =>
{
    var sandboxOptions = sp.GetRequiredService<IOptions<SandboxOptions>>().Value;

    var explicitHost = sandboxOptions.DockerHost;
    if (string.IsNullOrWhiteSpace(explicitHost))
    {
        explicitHost = Environment.GetEnvironmentVariable("DOCKER_HOST") ?? string.Empty;
    }

    var config = string.IsNullOrWhiteSpace(explicitHost) ? new DockerClientConfiguration() : new DockerClientConfiguration(new Uri(explicitHost));

    return config.CreateClient();
});

builder.Services.AddSingleton<ContainerConfigBuilder>();
builder.Services.AddSingleton<IJobDirectoryService, JobDirectoryService>();
builder.Services.AddSingleton<ISandboxRunner, DockerSandboxRunner>();
builder.Services.AddScoped<SandboxOrchestrator>();

builder.Services.AddTransient<InternalApiKeyDelegatingHandler>();

// Sentinel's PDP lives in Sentium.Web; every execution is authorized there first (fail-closed).
builder.Services.AddHttpClient<ISentinelGateway, HttpSentinelGateway>(client =>
{
    client.BaseAddress = new Uri($"https+http://{ServiceNames.Web}");
}).AddHttpMessageHandler<InternalApiKeyDelegatingHandler>();

builder.Services.AddHealthChecks().AddCheck<DockerHealthCheck>("docker");

var app = builder.Build();

app.UseSentiumTracing();
app.UseExceptionHandler();

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

namespace Sentium.Sandbox
{
    public partial class Program;
}
