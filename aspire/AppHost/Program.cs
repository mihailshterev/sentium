using AppHost.Constants;
using AppHost.Config;

var builder = DistributedApplication.CreateBuilder(args);

var nats = builder.AddNats(ResourceNames.NatsServiceName)
    .WithJetStream()
    .WithDataVolume();

var sqlPassword = builder.AddParameter("sql-password", secret: true);

var sql = builder.AddSqlServer(ResourceNames.SqlServerName, password: sqlPassword)
    .WithDataVolume();

var identityDb = sql.AddDatabase(ResourceNames.IdentityDbName);
var agentRuntimeDb = sql.AddDatabase(ResourceNames.AgentRuntimeDbName);

var ollama = builder.AddOllama(ResourceNames.OllamaServiceName)
    .WithDataVolume()
    .WithGPUSupport(OllamaGpuVendor.Nvidia)
    .WithEnvironment(OllamaConfig.ContextSizeKey, OllamaConfig.DefaultContextSize)
    .WithEnvironment(OllamaConfig.FlashAttentionKey, "1")
    .WithEnvironment(OllamaConfig.CacheTypeKey, "q4_0")
    .WithEnvironment(OllamaConfig.DebugKey, "1")
    .WithEndpoint("http", e => e.Port = 11434);

var ollamaModel = ollama.AddModel(AIModels.Qwen3_8B_Q4KM);

var identityApi = builder.AddProject<Projects.IdentityProvider_Api>(ServiceNames.Identity)
    .WithReference(identityDb).WaitFor(identityDb);

var baseDataDir = Path.Combine(builder.Environment.ContentRootPath, "data", "zeek");
var capturePath = Path.Combine(baseDataDir, "capture");
var logsPath = Path.Combine(baseDataDir, "logs");

Directory.CreateDirectory(capturePath);
Directory.CreateDirectory(logsPath);

var zeek = builder.AddContainer(ResourceNames.ZeekServiceName, "zeek/zeek")
    .WithBindMount(capturePath, "/capture")
    .WithBindMount(logsPath, "/output")
    .WithEntrypoint("sh")
    .WithArgs("-c", "cd /output && while true; do for f in /capture/*.pcap; do [ -e \"$f\" ] && zeek -C -r \"$f\" LogAscii::use_json=T local; done; sleep 5; done")
    .WithReference(nats)
    .WaitFor(nats);

var python = builder.AddPythonApp(ServiceNames.NetworkFilter, "../../services/NetworkFilter", "main.py")
    .WithEnvironment("NATS_SUBJECT_IN", "traffic.raw")
    .WithEnvironment("NATS_SUBJECT_OUT", "traffic.anomaly")
    .WithHttpEndpoint(port: 8000, env: "PORT")
    .WithReference(nats).WaitFor(nats)
    .WaitFor(zeek);

var sentinelApi = builder.AddProject<Projects.Sentinel_Api>(ServiceNames.Sentinel)
    .WithReference(nats).WaitFor(nats);

var agentRuntimeApi = builder.AddProject<Projects.AgentRuntime_Api>(ServiceNames.AgentRuntime)
    .WithReference(ollamaModel).WaitFor(ollamaModel)
    .WithReference(nats).WaitFor(nats)
    .WithReference(agentRuntimeDb).WaitFor(agentRuntimeDb)
    .WithEnvironment("AI__ModelName", ollamaModel.Resource.ModelName);

var apiGateway = builder.AddProject<Projects.ApiGateway>(ServiceNames.Gateway)
    .WithReference(identityApi).WaitFor(identityApi)
    .WithReference(sentinelApi).WaitFor(sentinelApi)
    .WithReference(agentRuntimeApi).WaitFor(agentRuntimeApi);

var frontend = builder.AddViteApp(ServiceNames.Frontend, "../../frontend")
    .WithReference(apiGateway).WaitFor(apiGateway)
    .WithEndpoint("http", e => e.Port = 5173);

identityApi.WithParentRelationship(apiGateway);
sentinelApi.WithParentRelationship(apiGateway);
agentRuntimeApi.WithParentRelationship(apiGateway);

builder.Build().Run();
