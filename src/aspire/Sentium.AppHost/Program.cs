using Sentium.AppHost.Config;
using Sentium.Shared.Constants;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("env");

var nats = builder.AddNats(ResourceNames.Nats)
    .WithJetStream()
    .WithDataVolume();

var redis = builder.AddRedis(ResourceNames.Redis);

var sqlPassword = builder.AddParameter("sql-password", secret: true);

var sql = builder.AddSqlServer(ResourceNames.Sql, password: sqlPassword)
    .WithDataVolume();

var identityDb = sql.AddDatabase(ResourceNames.IdentityDb);
var agentRuntimeDb = sql.AddDatabase(ResourceNames.AgentRuntimeDb);

var qdrant = builder.AddQdrant(ResourceNames.Qdrant)
    .WithDataVolume();

var storage = builder.AddAzureStorage(ResourceNames.Storage)
    .RunAsEmulator(azurite => azurite.WithDataVolume());

var blobs = storage.AddBlobs(ResourceNames.WorkspaceBlobs);

var ollama = builder.AddOllama(ResourceNames.Ollama)
    .WithImage("ollama/ollama", "0.20.2")
    .WithDataVolume()
    .WithGPUSupport(OllamaGpuVendor.Nvidia)
    // .WithEnvironment(OllamaConfig.ContextSizeKey, OllamaConfig.DefaultContextSize)
    .WithEnvironment(OllamaConfig.FlashAttentionKey, "1")
    .WithEnvironment(OllamaConfig.ParallelRequestsKey, "2")
    // .WithEnvironment(OllamaConfig.CacheTypeKey, "q4_0")
    .WithEnvironment(OllamaConfig.DebugKey, "1")
    .WithEndpoint("http", e => e.Port = 11434);

var ollamaModel = ollama.AddModel(AIModels.Gemma4);
var ollamaEmbeddingModel = ollama.AddModel(AIModels.NomicEmbedText);

var identityApi = builder.AddProject<Projects.Sentium_Identity_Api>(ServiceNames.Identity)
    .WithReference(identityDb).WaitFor(identityDb)
    .WithReference(nats).WaitFor(nats);

var baseDataDir = Path.Combine(builder.Environment.ContentRootPath, "data", "zeek");
var capturePath = Path.Combine(baseDataDir, "capture");
var logsPath = Path.Combine(baseDataDir, "logs");

Directory.CreateDirectory(capturePath);
Directory.CreateDirectory(logsPath);

// TODO: Configure host network traffic capturing
var zeek = builder.AddContainer(ResourceNames.Zeek, "zeek/zeek")
    .WithContainerRuntimeArgs("--cap-add", "NET_ADMIN", "--cap-add", "NET_RAW", "--network", "host", "--workdir", "/output")
    // .WithBindMount(capturePath, "/capture")
    .WithBindMount(logsPath, "/output")
    .WithEntrypoint("zeek")
    .WithArgs(
        "-i", "eth0",
        "-C",
        "local",
        "policy/tuning/json-logs.zeek",
        "LogAscii::use_json=T"
    )
    .WithReference(nats)
    .WaitFor(nats);

var python = builder.AddPythonApp(ServiceNames.NetworkFilter, "../../services/NetworkFilter", "main.py")
    .WithEnvironment("ZEEK_LOGS_PATH", logsPath)
    .WithHttpEndpoint(port: 8000, env: "PORT")
    .WithReference(nats).WaitFor(nats)
    .WaitFor(zeek);

var sentinelApi = builder.AddProject<Projects.Sentium_Sentinel_Api>(ServiceNames.Sentinel)
    .WithReference(nats).WaitFor(nats)
    .WithReference(identityApi).WaitFor(identityApi)
    .WithEnvironment("Identity__Authority", identityApi.GetEndpoint("http"));

var watchdogApi = builder.AddProject<Projects.Sentium_Watchdog_Api>(ServiceNames.Watchdog)
    .WithReference(nats).WaitFor(nats)
    .WithReference(identityApi).WaitFor(identityApi)
    .WithEnvironment("Identity__Authority", identityApi.GetEndpoint("http"));

var agentRuntimeApi = builder.AddProject<Projects.Sentium_AgentRuntime_Api>(ServiceNames.AgentRuntime)
    .WithReference(ollamaModel).WaitFor(ollamaModel)
    .WithReference(ollamaEmbeddingModel).WaitFor(ollamaEmbeddingModel)
    .WithReference(nats).WaitFor(nats)
    .WithReference(agentRuntimeDb).WaitFor(agentRuntimeDb)
    .WithReference(redis).WaitFor(redis)
    .WithReference(qdrant).WaitFor(qdrant)
    .WithReference(blobs).WaitFor(blobs)
    .WithReference(identityApi).WaitFor(identityApi)
    .WithEnvironment("AI__ModelName", ollamaModel.Resource.ModelName)
    .WithEnvironment("Rag__EmbeddingModelName", ollamaEmbeddingModel.Resource.ModelName)
    .WithEnvironment("Identity__Authority", identityApi.GetEndpoint("http"))
    .WithExternalHttpEndpoints()
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
    .WithEnvironment("Identity__Authority", identityApi.GetEndpoint("http"));

var frontend = builder.AddViteApp(ServiceNames.Frontend, "../../clients/sentium-portal")
    .WithPnpm()
    .WithReference(apiGateway).WaitFor(apiGateway)
    .WithEnvironment("VITE_API_BASE", apiGateway.GetEndpoint("https"))
    .WithEndpoint("http", e => e.Port = 5173);

identityApi.WithParentRelationship(apiGateway);
sentinelApi.WithParentRelationship(apiGateway);
watchdogApi.WithParentRelationship(apiGateway);
agentRuntimeApi.WithParentRelationship(apiGateway);

builder.Build().Run();
