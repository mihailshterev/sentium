using System.Runtime.InteropServices;
using Sentium.AppHost.Config;
using Sentium.Shared.Constants;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("env");

var nats = builder.AddNats(ResourceNames.Nats)
    .WithJetStream()
    .WithDataVolume();

var redis = builder.AddRedis(ResourceNames.Redis);

var sqlPassword = builder.AddParameter("sql-password", secret: true);
var internalApiKey = builder.AddParameter("internal-api-key", secret: true);
var gatewayBffSecret = builder.AddParameter("gateway-bff-secret", secret: true);
var serviceWorkerSecret = builder.AddParameter("service-worker-secret", secret: true);

var sql = builder.AddSqlServer(ResourceNames.Sql, password: sqlPassword)
    .WithDataVolume();

var identityDb = sql.AddDatabase(ResourceNames.IdentityDb);
var agentRuntimeDb = sql.AddDatabase(ResourceNames.AgentRuntimeDb);
var sandboxDb = sql.AddDatabase(ResourceNames.SandboxDb);
var sentinelDb = sql.AddDatabase(ResourceNames.SentinelDb);
var registryDb = sql.AddDatabase(ResourceNames.RegistryDb);

var qdrant = builder.AddQdrant(ResourceNames.Qdrant)
    .WithDataVolume();

var storage = builder.AddAzureStorage(ResourceNames.Storage)
    .RunAsEmulator(azurite => azurite.WithDataVolume());

var blobs = storage.AddBlobs(ResourceNames.WorkspaceBlobs);

var seq = builder.AddSeq(ResourceNames.Seq)
    .ExcludeFromManifest()
    .WithDataVolume()
    .WithEnvironment(EnvConfig.Keys.AcceptEula, EnvConfig.Values.Yes);

var ollama = builder.AddOllama(ResourceNames.Ollama)
    .WithImage("ollama/ollama", "0.30.5")
    .WithDataVolume()
    .WithGPUSupport(OllamaGpuVendor.Nvidia)
    // .WithEnvironment(OllamaConfig.ContextSizeKey, OllamaConfig.DefaultContextSize)
    .WithEnvironment(OllamaConfig.FlashAttentionKey, "1")
    .WithEnvironment(OllamaConfig.ParallelRequestsKey, "2")
    .WithEnvironment(OllamaConfig.KeepAliveKey, "-1")
    // .WithEnvironment(OllamaConfig.CacheTypeKey, "q4_0")
    .WithEnvironment(OllamaConfig.DebugKey, "1")
    .WithEndpoint("http", e => e.Port = 11434);

var ollamaModel = ollama.AddModel(AIModels.Gemma4);
var ollamaEmbeddingModel = ollama.AddModel(AIModels.NomicEmbedText);

var identityApi = builder.AddProject<Projects.Sentium_Identity_Api>(ServiceNames.Identity)
    .WithReference(identityDb).WaitFor(identityDb)
    .WithReference(nats).WaitFor(nats)
    .WithReference(seq).WaitFor(seq)
    .WithReference(redis).WaitFor(redis)
    .WithEnvironment("Identity__GatewayBffSecret", gatewayBffSecret)
    .WithEnvironment("Identity__ServiceWorkerSecret", serviceWorkerSecret)
    .WithUrlForEndpoint("https", url =>
    {
        url.DisplayText = "Scalar API (Docs)";
        url.Url = "/scalar/v1";
    });

var identityUi = builder.AddViteApp(ServiceNames.IdentityUi, "../../clients/sentium-identity-ui")
    .WithPnpm()
    .WithReference(identityApi).WaitFor(identityApi)
    .WithEnvironment(EnvConfig.Keys.Frontend.ViteIdentityApiBase, identityApi.GetEndpoint("https"))
    .WithEndpoint("http", e => e.Port = 5174);

identityUi.WithParentRelationship(identityApi);

var registryApi = builder.AddProject<Projects.Sentium_Registry_Api>(ServiceNames.Registry)
    .WithReference(registryDb).WaitFor(registryDb)
    .WithReference(nats).WaitFor(nats)
    .WithReference(redis).WaitFor(redis)
    .WithReference(seq).WaitFor(seq)
    .WithReference(identityApi).WaitFor(identityApi)
    .WithEnvironment(EnvConfig.Keys.IdentityAuthority, identityApi.GetEndpoint("http"))
    .WithEnvironment(EnvConfig.Keys.InternalApiKey, internalApiKey)
    .WithUrlForEndpoint("https", url =>
    {
        url.DisplayText = "Scalar API (Docs)";
        url.Url = "/scalar/v1";
    });

var sentinelApi = builder.AddProject<Projects.Sentium_Sentinel_Api>(ServiceNames.Sentinel)
    .WithReference(nats).WaitFor(nats)
    .WithReference(seq).WaitFor(seq)
    .WithReference(sentinelDb).WaitFor(sentinelDb)
    .WithReference(identityApi).WaitFor(identityApi)
    .WithReference(registryApi).WaitFor(registryApi)
    .WithReference(ollamaModel).WaitFor(ollamaModel)
    .WithEnvironment(EnvConfig.Keys.IdentityAuthority, identityApi.GetEndpoint("http"))
    .WithEnvironment(EnvConfig.Keys.InternalApiKey, internalApiKey)
    .WithUrlForEndpoint("https", url =>
    {
        url.DisplayText = "Scalar API (Docs)";
        url.Url = "/scalar/v1";
    });

var dockerHost = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
    ? EnvConfig.Values.DockerSockets.Windows
    : EnvConfig.Values.DockerSockets.LinuxMac;

var sandboxApi = builder.AddProject<Projects.Sentium_Sandbox_Api>(ServiceNames.Sandbox)
    .WithReference(nats).WaitFor(nats)
    .WithReference(seq).WaitFor(seq)
    .WithReference(blobs).WaitFor(blobs)
    .WithReference(sandboxDb).WaitFor(sandboxDb)
    .WithReference(sentinelApi).WaitFor(sentinelApi)
    .WithReference(identityApi).WaitFor(identityApi)
    .WithEnvironment(EnvConfig.Keys.IdentityAuthority, identityApi.GetEndpoint("http"))
    .WithEnvironment(EnvConfig.Keys.DockerHost, dockerHost)
    .WithEnvironment(EnvConfig.Keys.InternalApiKey, internalApiKey)
    .WithUrlForEndpoint("https", url =>
    {
        url.DisplayText = "Scalar API (Docs)";
        url.Url = "/scalar/v1";
    });

var agentRuntimeApi = builder.AddProject<Projects.Sentium_AgentRuntime_Api>(ServiceNames.AgentRuntime)
    .WithReference(ollamaModel).WaitFor(ollamaModel)
    .WithReference(ollamaEmbeddingModel).WaitFor(ollamaEmbeddingModel)
    .WithReference(nats).WaitFor(nats)
    .WithReference(seq).WaitFor(seq)
    .WithReference(agentRuntimeDb).WaitFor(agentRuntimeDb)
    .WithReference(redis).WaitFor(redis)
    .WithReference(qdrant).WaitFor(qdrant)
    .WithReference(blobs).WaitFor(blobs)
    .WithReference(identityApi).WaitFor(identityApi)
    .WithReference(sentinelApi).WaitFor(sentinelApi)
    .WithReference(sandboxApi).WaitFor(sandboxApi)
    .WithReference(registryApi).WaitFor(registryApi)
    .WithEnvironment(EnvConfig.Keys.AI.ModelName, ollamaModel.Resource.ModelName)
    .WithEnvironment(EnvConfig.Keys.AI.EmbeddingModelName, ollamaEmbeddingModel.Resource.ModelName)
    .WithEnvironment(EnvConfig.Keys.IdentityAuthority, identityApi.GetEndpoint("http"))
    .WithEnvironment(EnvConfig.Keys.InternalApiKey, internalApiKey)
    .WithExternalHttpEndpoints()
    .WithUrlForEndpoint("https", url =>
    {
        url.DisplayText = "Scalar API (Docs)";
        url.Url = "/scalar/v1";
    });

var watchdogApi = builder.AddProject<Projects.Sentium_Watchdog_Api>(ServiceNames.Watchdog)
    .WithReference(nats).WaitFor(nats)
    .WithReference(seq).WaitFor(seq)
    .WithReference(sql).WaitFor(sql)
    .WithReference(redis).WaitFor(redis)
    .WithReference(identityApi).WaitFor(identityApi)
    .WithReference(sentinelApi).WaitFor(sentinelApi)
    .WithReference(agentRuntimeApi).WaitFor(agentRuntimeApi)
    .WithEnvironment(EnvConfig.Keys.IdentityAuthority, identityApi.GetEndpoint("http"))
    .WithUrlForEndpoint("https", url =>
    {
        url.DisplayText = "Scalar API (Docs)";
        url.Url = "/scalar/v1";
    });

var apiGateway = builder.AddProject<Projects.Sentium_ApiGateway>(ServiceNames.Gateway)
    .WithReference(identityApi).WaitFor(identityApi)
    .WithReference(sentinelApi).WaitFor(sentinelApi)
    .WithReference(watchdogApi).WaitFor(watchdogApi)
    .WithReference(agentRuntimeApi).WaitFor(agentRuntimeApi)
    .WithReference(sandboxApi).WaitFor(sandboxApi)
    .WithReference(registryApi).WaitFor(registryApi)
    .WithEnvironment(EnvConfig.Keys.IdentityAuthority, identityApi.GetEndpoint("http"))
    .WithEnvironment("Identity__GatewayBffSecret", gatewayBffSecret);

var frontend = builder.AddViteApp(ServiceNames.Frontend, "../../clients/sentium-portal")
    .WithPnpm()
    .WithReference(apiGateway).WaitFor(apiGateway)
    .WithEnvironment(EnvConfig.Keys.Frontend.ViteApiBase, apiGateway.GetEndpoint("https"))
    .WithEndpoint("http", e => e.Port = 5173);

identityApi.WithParentRelationship(apiGateway);
sentinelApi.WithParentRelationship(apiGateway);
watchdogApi.WithParentRelationship(apiGateway);
agentRuntimeApi.WithParentRelationship(apiGateway);
sandboxApi.WithParentRelationship(apiGateway);
registryApi.WithParentRelationship(apiGateway);

builder.Build().Run();
