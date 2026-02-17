using AppHost.Constants;
using AppHost.Config;

var builder = DistributedApplication.CreateBuilder(args);

var nats = builder.AddNats(ResourceNames.NatsServiceName)
    .WithJetStream()
    .WithDataVolume();

var ollama = builder.AddOllama(ResourceNames.OllamaServiceName)
    .WithDataVolume()
    .WithGPUSupport(OllamaGpuVendor.Nvidia)
    .WithEnvironment(OllamaConfig.ContextSizeKey, OllamaConfig.DefaultContextSize)
    .WithEnvironment(OllamaConfig.FlashAttentionKey, "1")
    .WithEnvironment(OllamaConfig.CacheTypeKey, "q4_0")
    .WithEnvironment(OllamaConfig.DebugKey, "1")
    .WithEndpoint("http", e => e.Port = 11434);
//.WithOpenWebUI();

var ollamaModel = ollama.AddModel(AIModels.Qwen3);

var identityApi = builder.AddProject<Projects.IdentityProvider_Api>(ServiceNames.Identity);
var sentinelApi = builder.AddProject<Projects.Sentinel_Api>(ServiceNames.Sentinel)
    .WithReference(nats).WaitFor(nats);
var agentRuntimeApi = builder.AddProject<Projects.AgentRuntime_Api>(ServiceNames.AgentRuntime)
    .WithReference(ollamaModel).WaitFor(ollamaModel)
    .WithReference(nats).WaitFor(nats)
    .WithEnvironment("AI__ModelName", ollamaModel.Resource.ModelName);

var apiGateway = builder.AddProject<Projects.ApiGateway>(ServiceNames.Gateway)
    .WithReference(identityApi).WaitFor(identityApi)
    .WithReference(sentinelApi).WaitFor(sentinelApi)
    .WithReference(agentRuntimeApi).WaitFor(agentRuntimeApi);

var frontend = builder.AddViteApp(ServiceNames.Frontend, "../../frontend")
    .WithReference(apiGateway).WaitFor(apiGateway);

identityApi.WithParentRelationship(apiGateway);
sentinelApi.WithParentRelationship(apiGateway);
agentRuntimeApi.WithParentRelationship(apiGateway);

builder.Build().Run();
