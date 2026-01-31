using AppHost;

var builder = DistributedApplication.CreateBuilder(args);

var nats = builder.AddNats("nats")
    .WithDataVolume();

var ollama = builder.AddOllama(ServiceConstants.OllamaServiceName)
    .WithDataVolume()
    .WithGPUSupport(OllamaGpuVendor.Nvidia);
//.WithOpenWebUI();

var gemma31b = ollama.AddModel(ServiceConstants.Gemma3ModelName);

var identityApi = builder.AddProject<Projects.IdentityProvider_Api>(ServiceNames.Identity);
var sentinelApi = builder.AddProject<Projects.Sentinel_Api>(ServiceNames.Sentinel)
    .WithReference(nats).WaitFor(nats);
var agentRuntimeApi = builder.AddProject<Projects.AgentRuntime_Api>(ServiceNames.AgentRuntime)
    .WithReference(gemma31b).WaitFor(gemma31b)
    .WithReference(nats).WaitFor(nats);

builder.AddProject<Projects.ApiGateway>(ServiceNames.Gateway)
    .WithReference(identityApi).WaitFor(identityApi)
    .WithReference(sentinelApi).WaitFor(sentinelApi)
    .WithReference(agentRuntimeApi).WaitFor(agentRuntimeApi);

builder.Build().Run();
