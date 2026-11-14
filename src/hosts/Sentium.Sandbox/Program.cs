using Docker.DotNet;
using FluentValidation;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Sentium.Core.Constants;
using Sentium.Infrastructure.Diagnostics;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Mvc;
using Sentium.Infrastructure.Security;
using Sentium.Infrastructure.Validation;
using Sentium.Sandbox.Artifacts;
using Sentium.Sandbox.Docker;
using Sentium.Sandbox.Execution;
using Sentium.Sandbox.Mcp;
using Sentium.Sandbox.Options;
using Sentium.Sandbox.Sentinel;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSentiumAuditLogging();
builder.AddInternalApiSecurity();

builder.Services.AddSentiumProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddControllers(options => options.Filters.Add<FluentValidationFilter>())
    .AddInternalControllers();
builder.Services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);

builder.Services.Configure<SandboxOptions>(builder.Configuration.GetSection(SandboxOptions.SectionName));
builder.Services.Configure<McpHostOptions>(builder.Configuration.GetSection(McpHostOptions.SectionName));

builder.AddAzureBlobServiceClient(ResourceNames.WorkspaceBlobs);
builder.Services.AddSingleton<IArtifactService, ArtifactService>();

builder.Services.AddSingleton<IDockerClient>(sp =>
{
    var endpoint = DockerEndpoint.Resolve(sp.GetRequiredService<IOptions<SandboxOptions>>().Value);
    return new DockerClientConfiguration(endpoint).CreateClient();
});

builder.Services.AddSingleton<ContainerConfigBuilder>();
builder.Services.AddSingleton<DockerImageService>();
builder.Services.AddSingleton<IJobDirectoryService, JobDirectoryService>();
builder.Services.AddSingleton<ISandboxRunner, DockerSandboxRunner>();
builder.Services.AddScoped<SandboxOrchestrator>();

// Stdio MCP servers in hardened long-lived containers, on behalf of Sentium.Web (off unless Sandbox:Mcp:Enabled).
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DockerStdioAttach>();
builder.Services.AddSingleton<McpContainerManager>();
builder.Services.AddSingleton<IMcpServerHost>(sp => sp.GetRequiredService<McpContainerManager>());
builder.Services.AddHostedService<McpHostService>();

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
