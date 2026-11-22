using Docker.DotNet;
using FluentValidation;
using Microsoft.Extensions.Options;
using Sentium.Core.Constants;
using Sentium.Infrastructure.Diagnostics;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Mvc;
using Sentium.Infrastructure.Validation;
using Sentium.Sandbox.Artifacts;
using Sentium.Sandbox.Docker;
using Sentium.Sandbox.Execution;
using Sentium.Sandbox.Mcp;
using Sentium.Sandbox.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSentiumConsoleLogging();
builder.AddInternalApiSecurity();

builder.Services.AddSentiumProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddControllers(options => options.Filters.Add<FluentValidationFilter>())
    .AddInternalControllers();
builder.Services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);

builder.Services.Configure<SandboxOptions>(builder.Configuration.GetSection(SandboxOptions.SectionName));
builder.Services.Configure<McpHostOptions>(builder.Configuration.GetSection(McpHostOptions.SectionName));

builder.AddSentiumObjectStore(ResourceNames.WorkspaceBlobs);
builder.Services.AddSingleton<IArtifactService, ArtifactService>();

builder.Services.AddSingleton<IDockerClient>(sp =>
{
    var endpoint = DockerEndpoint.Resolve(sp.GetRequiredService<IOptions<SandboxOptions>>().Value);
    return new DockerClientBuilder().WithEndpoint(endpoint).Build();
});

builder.Services.AddSingleton<ContainerConfigBuilder>();
builder.Services.AddSingleton<DockerImageService>();
builder.Services.AddSingleton<IJobDirectoryService, JobDirectoryService>();
builder.Services.AddSingleton<ISandboxRunner, DockerSandboxRunner>();

// Stdio MCP servers in hardened long-lived containers, on behalf of Sentium.Web (off unless Sandbox:Mcp:Enabled).
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DockerStdioAttach>();
builder.Services.AddSingleton<McpContainerManager>();
builder.Services.AddSingleton<IMcpServerHost>(sp => sp.GetRequiredService<McpContainerManager>());
builder.Services.AddHostedService<McpHostService>();

builder.Services.AddHealthChecks().AddCheck<DockerHealthCheck>("docker");

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
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
