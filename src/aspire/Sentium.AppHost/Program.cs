using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
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

// Local inference: llama.cpp's llama-server, as a router for the chat models declared in infra/llm/models.ini
// plus a dedicated embeddings server. Model files are fetched and checksum-verified from
// infra/llm/models.lock by a one-shot init container into a volume shared with Docker Compose.
var useGpu = builder.Configuration.GetValue(LlamaConfig.UseGpuKey, defaultValue: true);

var llmApiKey = builder.AddParameter(
    "llm-api-key",
    new GenerateParameterDefault { MinLength = 32, Special = false },
    secret: true,
    persist: true);

var modelsInit = builder.AddContainer(ResourceNames.ModelsInit, LlamaConfig.InitImage, LlamaConfig.InitTag)
    .WithImageSHA256(LlamaConfig.InitDigest)
    .WithBindMount(LlamaConfig.InfraSource, LlamaConfig.InfraPath, isReadOnly: true)
    .WithVolume(LlamaConfig.ModelsVolume, LlamaConfig.ModelsPath)
    .WithContainerRuntimeArgs("--user", "0:0")
    .WithEntrypoint("sh")
    .WithArgs($"{LlamaConfig.InfraPath}/fetch-models.sh");

var llm = AddLlamaServer(ResourceNames.Llm, LlamaConfig.LlmHostPort)
    .WithBindMount(LlamaConfig.InfraSource, LlamaConfig.InfraPath, isReadOnly: true)
    .WithArgs("--models-preset", $"{LlamaConfig.InfraPath}/models.ini", "--models-max", "2");

const string llmReadyCheck = "llm-model-loaded";
builder.Services.AddHealthChecks().Add(new HealthCheckRegistration(
    llmReadyCheck,
    _ => new LlamaRouterReadyHealthCheck(llm.GetEndpoint("http"), llmApiKey.Resource),
    failureStatus: null,
    tags: null));
llm.WithHealthCheck(llmReadyCheck);

var embeddings = AddLlamaServer(ResourceNames.Embeddings, LlamaConfig.EmbeddingsHostPort)
    .WithArgs(
        "--model", $"{LlamaConfig.ModelsPath}/{LlamaConfig.EmbeddingModelFile}",
        "--embeddings",
        "--ctx-size", "8192",
        "--batch-size", "2048",
        "--ubatch-size", "2048",
        "--parallel", "4");

// Speech for the Companion: whisper.cpp (speech-to-text, model from the same verified volume) and Kokoro
// (text-to-speech, model baked into the image). Optional: Sentium.Web starts without waiting for them.
var useSpeech = builder.Configuration.GetValue(SpeechConfig.EnabledKey, defaultValue: true);

var whisper = useSpeech
    ? builder.AddContainer(ResourceNames.Whisper, SpeechConfig.WhisperImage, SpeechConfig.WhisperTag)
        .WithImageRegistry(SpeechConfig.WhisperRegistry)
        .WithImageSHA256(SpeechConfig.WhisperDigest)
        .WithHttpEndpoint(port: SpeechConfig.WhisperHostPort, targetPort: SpeechConfig.WhisperContainerPort, name: "http")
        .WithVolume(LlamaConfig.ModelsVolume, LlamaConfig.ModelsPath, isReadOnly: true)
        .WithEntrypoint(SpeechConfig.WhisperServerPath)
        .WithArgs(
            "--host", "0.0.0.0",
            "--port", SpeechConfig.WhisperContainerPort.ToString(CultureInfo.InvariantCulture),
            "--model", $"{LlamaConfig.ModelsPath}/{SpeechConfig.WhisperModelFile}",
            "--inference-path", SpeechConfig.WhisperInferencePath,
            "--convert",
            "--tmp-dir", "/tmp",
            "--no-timestamps",
            "--vad",
            "--vad-model", $"{LlamaConfig.ModelsPath}/{SpeechConfig.WhisperVadModelFile}")
        .WithHttpHealthCheck("/health")
        .WaitForCompletion(modelsInit)
    : null;

var tts = useSpeech
    ? builder.AddContainer(ResourceNames.Tts, SpeechConfig.TtsImage, SpeechConfig.TtsTag)
        .WithImageRegistry(SpeechConfig.TtsRegistry)
        .WithImageSHA256(SpeechConfig.TtsDigest)
        .WithHttpEndpoint(port: SpeechConfig.TtsHostPort, targetPort: SpeechConfig.TtsContainerPort, name: "http")
        .WithHttpHealthCheck("/health")
    : null;

var useBackup = builder.Configuration.GetValue(BackupConfig.EnabledKey, defaultValue: true);

var backup = useBackup ? AddBackup() : null;

var dockerHost = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
    ? EnvConfig.Values.DockerSockets.Windows
    : EnvConfig.Values.DockerSockets.LinuxMac;

var web = builder.AddProject<Projects.Sentium_Web>(ServiceNames.Web)
    .WithReference(sentiumDb).WaitFor(sentiumDb)
    .WithReference(postgres)
    .WithReference(redis).WaitFor(redis)
    .WithReference(blobs).WaitFor(blobs)
    .WaitFor(llm)
    .WaitFor(embeddings)
    .WithEnvironment(EnvConfig.Keys.Inference.ChatEndpoint, llm.GetEndpoint("http"))
    .WithEnvironment(EnvConfig.Keys.Inference.EmbeddingEndpoint, embeddings.GetEndpoint("http"))
    .WithEnvironment(EnvConfig.Keys.Inference.ApiKey, llmApiKey)
    .WithEnvironment(EnvConfig.Keys.Inference.DefaultModel, AIModels.Gemma4_E4B)
    .WithEnvironment(EnvConfig.Keys.Inference.EmbeddingModelName, AIModels.EmbeddingGemma2)
    .WithEnvironment(EnvConfig.Keys.InternalApiKey, internalApiKey)
    .WithExternalHttpEndpoints()
    .WithUrlForEndpoint("https", url =>
    {
        url.DisplayText = "Scalar API (Docs)";
        url.Url = "/scalar/v1";
    });

if (backup is not null)
{
    web.WithEnvironment(EnvConfig.Keys.Backup.Endpoint, backup.GetEndpoint("http"));
    backup.WithParentRelationship(web);
}

if (whisper is not null && tts is not null)
{
    web.WithEnvironment(EnvConfig.Keys.Speech.TranscriptionEndpoint, whisper.GetEndpoint("http"))
        .WithEnvironment(EnvConfig.Keys.Speech.SynthesisEndpoint, tts.GetEndpoint("http"));
}

var egressNetwork = builder.Configuration[SearxngConfig.EgressNetworkKey] ?? SearxngConfig.EgressNetwork;

var sandbox = builder.AddProject<Projects.Sentium_Sandbox>(ServiceNames.Sandbox)
    .WithReference(blobs).WaitFor(blobs)
    .WithEnvironment(EnvConfig.Keys.DockerHost, dockerHost)
    .WithEnvironment(EnvConfig.Keys.InternalApiKey, internalApiKey)
    .WithEnvironment(EnvConfig.Keys.SandboxMcp.Enabled, builder.Configuration["Mcp:SandboxEnabled"] ?? "true")
    .WithEnvironment(EnvConfig.Keys.SandboxMcp.AllowedNetwork, egressNetwork);

web.WithReference(sandbox);
sandbox.WithParentRelationship(web);

if (builder.ExecutionContext.IsRunMode)
{
    builder.Eventing.Subscribe<BeforeStartEvent>((_, ct) => InternalNetwork.EnsureAsync(egressNetwork, ct));
}

var searxngSecret = builder.AddParameter(
    "searxng-secret",
    new GenerateParameterDefault { MinLength = 32, Special = false },
    secret: true,
    persist: true);

builder.AddContainer(ResourceNames.Searxng, SearxngConfig.Image)
    .WithImageSHA256(SearxngConfig.Digest)
    .WithHttpEndpoint(targetPort: SearxngConfig.ContainerPort, name: "http")
    .WithBindMount(SearxngConfig.SettingsSource, SearxngConfig.SettingsPath, isReadOnly: true)
    .WithEnvironment("SEARXNG_SECRET", searxngSecret)
    .WithContainerRuntimeArgs("--network", $"name={egressNetwork},alias={ResourceNames.Searxng}")
    .WithHttpHealthCheck("/healthz")
    .WithParentRelationship(sandbox);

builder.AddViteApp(ServiceNames.Frontend, "../../clients/sentium-portal")
    .WithPnpm()
    .WithReference(web).WaitFor(web)
    .WithEnvironment(EnvConfig.Keys.Frontend.ApiProxyTarget, web.GetEndpoint("https"))
    .WithEndpoint("http", e => e.Port = 5173);

builder.Build().Run();

IResourceBuilder<ContainerResource> AddBackup()
{
    var destination = Path.GetFullPath(
        builder.Configuration[BackupConfig.DestinationKey] ?? BackupConfig.DefaultDestination,
        builder.AppHostDirectory);

    // The marker tells the sidecar the destination is the real one; a local dev directory always is.
    if (builder.ExecutionContext.IsRunMode)
    {
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(destination, BackupConfig.MarkerFileName), string.Empty);
    }

    return builder.AddDockerfile(ResourceNames.Backup, BackupConfig.ContextPath, BackupConfig.DockerfilePath)
        .WithHttpEndpoint(targetPort: BackupConfig.ContainerPort, name: "http")
        .WithReference(sentiumDb).WaitFor(sentiumDb)
        .WithReference(blobs).WaitFor(blobs)
        .WithEnvironment(EnvConfig.Keys.InternalApiKey, internalApiKey)
        .WithBindMount(destination, BackupConfig.DestinationPath)
        .WithVolume(BackupConfig.KeysVolume, BackupConfig.KeysPath, isReadOnly: true)
        .WithOtlpExporter()
        .WithHttpHealthCheck("/health");
}

IResourceBuilder<ContainerResource> AddLlamaServer(string name, int hostPort)
{
    var server = builder.AddContainer(name, LlamaConfig.Image, useGpu ? LlamaConfig.GpuTag : LlamaConfig.CpuTag)
        .WithImageRegistry(LlamaConfig.Registry)
        .WithImageSHA256(useGpu ? LlamaConfig.GpuDigest : LlamaConfig.CpuDigest)
        .WithHttpEndpoint(port: hostPort, targetPort: LlamaConfig.ContainerPort, name: "http")
        .WithVolume(LlamaConfig.ModelsVolume, LlamaConfig.ModelsPath, isReadOnly: true)
        .WithEnvironment(EnvConfig.Keys.Inference.ServerApiKey, llmApiKey)
        .WithArgs("--no-webui", "--metrics")
        .WithHttpHealthCheck("/health")
        .WaitForCompletion(modelsInit);

    return useGpu ? server.WithContainerRuntimeArgs("--gpus", "all") : server;
}
