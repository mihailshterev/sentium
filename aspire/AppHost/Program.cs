using AppHost.Constants;
using AppHost.Config;

var builder = DistributedApplication.CreateBuilder(args);

var nats = builder.AddNats(ResourceNames.NatsServiceName)
    .WithJetStream()
    .WithDataVolume();

var redis = builder.AddRedis("redis");

var sqlPassword = builder.AddParameter("sql-password", secret: true);

var sql = builder.AddSqlServer(ResourceNames.SqlServerName, password: sqlPassword)
    .WithDataVolume();

var identityDb = sql.AddDatabase(ResourceNames.IdentityDbName);
var agentRuntimeDb = sql.AddDatabase(ResourceNames.AgentRuntimeDbName);

var ollama = builder.AddOllama(ResourceNames.OllamaServiceName)
    .WithImage("ollama/ollama", "0.20.2")
    .WithDataVolume()
    .WithGPUSupport(OllamaGpuVendor.Nvidia)
    // .WithEnvironment(OllamaConfig.ContextSizeKey, OllamaConfig.DefaultContextSize)
    .WithEnvironment(OllamaConfig.FlashAttentionKey, "1")
    // .WithEnvironment(OllamaConfig.CacheTypeKey, "q4_0")
    .WithEnvironment(OllamaConfig.DebugKey, "1")
    .WithEndpoint("http", e => e.Port = 11434);

var ollamaModel = ollama.AddModel(AIModels.Gemma4);

var identityApi = builder.AddProject<Projects.IdentityProvider_Api>(ServiceNames.Identity)
    .WithReference(identityDb).WaitFor(identityDb)
    .WithReference(nats).WaitFor(nats);

var baseDataDir = Path.Combine(builder.Environment.ContentRootPath, "data", "zeek");
var capturePath = Path.Combine(baseDataDir, "capture");
var logsPath = Path.Combine(baseDataDir, "logs");

Directory.CreateDirectory(capturePath);
Directory.CreateDirectory(logsPath);

// TODO: Configure host network traffic capturing
var zeek = builder.AddContainer(ResourceNames.ZeekServiceName, "zeek/zeek")
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

var sentinelApi = builder.AddProject<Projects.Sentinel_Api>(ServiceNames.Sentinel)
    .WithReference(nats).WaitFor(nats)
    .WithReference(identityApi).WaitFor(identityApi)
    .WithEnvironment("Identity__Authority", identityApi.GetEndpoint("http"));

var watchdogApi = builder.AddProject<Projects.Watchdog_Api>(ServiceNames.Watchdog)
    .WithReference(nats).WaitFor(nats)
    .WithReference(identityApi).WaitFor(identityApi)
    .WithEnvironment("Identity__Authority", identityApi.GetEndpoint("http"));

var agentRuntimeApi = builder.AddProject<Projects.AgentRuntime_Api>(ServiceNames.AgentRuntime)
    .WithReference(ollamaModel).WaitFor(ollamaModel)
    .WithReference(nats).WaitFor(nats)
    .WithReference(agentRuntimeDb).WaitFor(agentRuntimeDb)
    .WithReference(redis).WaitFor(redis)
    .WithReference(identityApi).WaitFor(identityApi)
    .WithEnvironment("AI__ModelName", ollamaModel.Resource.ModelName)
    .WithEnvironment("Identity__Authority", identityApi.GetEndpoint("http"));

var apiGateway = builder.AddProject<Projects.ApiGateway>(ServiceNames.Gateway)
    .WithReference(identityApi).WaitFor(identityApi)
    .WithReference(sentinelApi).WaitFor(sentinelApi)
    .WithReference(watchdogApi).WaitFor(watchdogApi)
    .WithReference(agentRuntimeApi).WaitFor(agentRuntimeApi)
    .WithEnvironment("Identity__Authority", identityApi.GetEndpoint("http"));

var frontend = builder.AddViteApp(ServiceNames.Frontend, "../../frontend")
    .WithPnpm()
    .WithReference(apiGateway).WaitFor(apiGateway)
    .WithEnvironment("VITE_API_BASE", apiGateway.GetEndpoint("https"))
    .WithEndpoint("http", e => e.Port = 5173);

identityApi.WithParentRelationship(apiGateway);
sentinelApi.WithParentRelationship(apiGateway);
watchdogApi.WithParentRelationship(apiGateway);
agentRuntimeApi.WithParentRelationship(apiGateway);

builder.Build().Run();
