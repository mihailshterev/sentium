using System.Runtime.InteropServices;
using Sentium.AppHost.Config;
using Sentium.Core.Constants;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("env");

var redis = builder.AddRedis(ResourceNames.Redis);

var internalApiKey = builder.AddParameter("internal-api-key", secret: true);

var postgres = builder.AddPostgres(ResourceNames.Postgres)
    .WithImage("pgvector/pgvector", "pg18-trixie");

postgres.WithVolume(VolumeNameGenerator.Generate(postgres, "data"), "/var/lib/postgresql");

var sentiumDb = postgres.AddDatabase(ResourceNames.SentiumDb);

var storage = builder.AddAzureStorage(ResourceNames.Storage)
    .RunAsEmulator(azurite => azurite.WithDataVolume());

var blobs = storage.AddBlobs(ResourceNames.WorkspaceBlobs);

var ollama = builder.AddOllama(ResourceNames.Ollama)
    .WithImage("ollama/ollama", "0.40.6")
    .WithEndpoint("http", e => e.Port = 11434)
    .WithDataVolume()
    .WithGPUSupport(OllamaGpuVendor.Nvidia)
    .WithEnvironment(OllamaConfig.FlashAttentionKey, "1")
    .WithEnvironment(OllamaConfig.ParallelRequestsKey, "2")
    .WithEnvironment(OllamaConfig.KeepAliveKey, "-1")
    .WithEnvironment(OllamaConfig.ContextSizeKey, OllamaConfig.DefaultContextSize)
    .WithEnvironment(OllamaConfig.DebugKey, "1");

var ollamaModel = ollama.AddModel(AIModels.Gemma4_E4B);
var ollamaEmbeddingModel = ollama.AddModel(AIModels.EmbeddingGemma2_740M);

var dockerHost = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
    ? EnvConfig.Values.DockerSockets.Windows
    : EnvConfig.Values.DockerSockets.LinuxMac;

var web = builder.AddProject<Projects.Sentium_Web>(ServiceNames.Web)
    .WithReference(sentiumDb).WaitFor(sentiumDb)
    .WithReference(postgres)
    .WithReference(redis).WaitFor(redis)
    .WithReference(blobs).WaitFor(blobs)
    .WithReference(ollama)
    .WithReference(ollamaModel).WaitFor(ollamaModel)
    .WithReference(ollamaEmbeddingModel).WaitFor(ollamaEmbeddingModel)
    .WithEnvironment(EnvConfig.Keys.Inference.ChatEndpoint, ollama.Resource.PrimaryEndpoint)
    .WithEnvironment(EnvConfig.Keys.Inference.EmbeddingEndpoint, ollama.Resource.PrimaryEndpoint)
    .WithEnvironment(EnvConfig.Keys.Inference.DefaultModel, ollamaModel.Resource.ModelName)
    .WithEnvironment(EnvConfig.Keys.Inference.EmbeddingModelName, ollamaEmbeddingModel.Resource.ModelName)
    .WithEnvironment(EnvConfig.Keys.InternalApiKey, internalApiKey)
    .WithExternalHttpEndpoints()
    .WithUrlForEndpoint("https", url =>
    {
        url.DisplayText = "Scalar API (Docs)";
        url.Url = "/scalar/v1";
    });

var sandbox = builder.AddProject<Projects.Sentium_Sandbox>(ServiceNames.Sandbox)
    .WithReference(blobs).WaitFor(blobs)
    .WithReference(web)
    .WithEnvironment(EnvConfig.Keys.DockerHost, dockerHost)
    .WithEnvironment(EnvConfig.Keys.InternalApiKey, internalApiKey);

web.WithReference(sandbox);
sandbox.WithParentRelationship(web);

builder.AddViteApp(ServiceNames.Frontend, "../../clients/sentium-portal")
    .WithPnpm()
    .WithReference(web).WaitFor(web)
    .WithEnvironment(EnvConfig.Keys.Frontend.ApiProxyTarget, web.GetEndpoint("https"))
    .WithEndpoint("http", e => e.Port = 5173);

builder.Build().Run();
