using System.Runtime.InteropServices;
using Sentium.AppHost.Config;
using Sentium.Core.Constants;

internal static class MonolithConfiguration
{
    private const string TestingEnvironment = "Testing";

    internal static void Configure(IDistributedApplicationBuilder builder, bool testRun)
    {
        if (!testRun)
        {
            builder.AddDockerComposeEnvironment("env");
        }

        var nats = builder.AddNats(ResourceNames.Nats)
            .WithJetStream();

        var redis = builder.AddRedis(ResourceNames.Redis);

        var internalApiKey = builder.AddParameter("internal-api-key", secret: true);

        var postgres = builder.AddPostgres(ResourceNames.Postgres);

        var sentiumDb = postgres.AddDatabase(ResourceNames.SentiumDb, testRun ? "sentiumdb_e2e" : ResourceNames.SentiumDb);

        var qdrant = builder.AddQdrant(ResourceNames.Qdrant);

        var storage = builder.AddAzureStorage(ResourceNames.Storage)
            .RunAsEmulator(azurite =>
            {
                if (!testRun)
                {
                    azurite.WithDataVolume();
                }
            });

        var blobs = storage.AddBlobs(ResourceNames.WorkspaceBlobs);

        var seq = builder.AddSeq(ResourceNames.Seq)
            .ExcludeFromManifest()
            .WithEnvironment(EnvConfig.Keys.AcceptEula, EnvConfig.Values.Yes);

        var ollama = builder.AddOllama(ResourceNames.Ollama)
            .WithImage("ollama/ollama", "0.40.6")
            .WithEndpoint("http", e => e.Port = 11434);

        if (!testRun)
        {
            nats.WithDataVolume();
            postgres.WithDataVolume();
            qdrant.WithDataVolume();
            seq.WithDataVolume();

            ollama.WithDataVolume()
                .WithGPUSupport(OllamaGpuVendor.Nvidia)
                .WithEnvironment(OllamaConfig.FlashAttentionKey, "1")
                .WithEnvironment(OllamaConfig.ParallelRequestsKey, "2")
                .WithEnvironment(OllamaConfig.KeepAliveKey, "-1")
                .WithEnvironment(OllamaConfig.DebugKey, "1");
        }

        var dockerHost = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? EnvConfig.Values.DockerSockets.Windows
            : EnvConfig.Values.DockerSockets.LinuxMac;

        var web = builder.AddProject<Projects.Sentium_Web>(ServiceNames.Web)
            .WithReference(sentiumDb).WaitFor(sentiumDb)
            .WithReference(postgres)
            .WithReference(nats).WaitFor(nats)
            .WithReference(redis).WaitFor(redis)
            .WithReference(qdrant).WaitFor(qdrant)
            .WithReference(blobs).WaitFor(blobs)
            .WithReference(seq).WaitFor(seq)
            .WithReference(ollama)
            .WithEnvironment(EnvConfig.Keys.InternalApiKey, internalApiKey)
            .WithExternalHttpEndpoints()
            .WithUrlForEndpoint("https", url =>
            {
                url.DisplayText = "Scalar API (Docs)";
                url.Url = "/scalar/v1";
            });

        // Sandbox calls Sentinel in Sentium.Web; Praxis calls Sandbox. References in both directions are
        // service discovery only - no WaitFor, so there is no startup cycle.
        var sandbox = builder.AddProject<Projects.Sentium_Sandbox>(ServiceNames.Sandbox)
            .WithReference(blobs).WaitFor(blobs)
            .WithReference(seq).WaitFor(seq)
            .WithReference(web)
            .WithEnvironment(EnvConfig.Keys.DockerHost, dockerHost)
            .WithEnvironment(EnvConfig.Keys.InternalApiKey, internalApiKey);

        web.WithReference(sandbox);
        sandbox.WithParentRelationship(web);

        if (testRun)
        {
            sandbox.WithEnvironment("ASPNETCORE_ENVIRONMENT", TestingEnvironment);

            // E2E runs without pulled models: the portal's model selector is free text.
            web.WaitFor(ollama)
                .WithEnvironment("ASPNETCORE_ENVIRONMENT", TestingEnvironment)
                .WithEnvironment(EnvConfig.Keys.AI.ModelName, AIModels.Gemma4_E4B)
                .WithEnvironment(EnvConfig.Keys.AI.EmbeddingModelName, AIModels.EmbeddingGemma2_740M);
        }
        else
        {
            var ollamaModel = ollama.AddModel(AIModels.Gemma4_E4B);
            var ollamaEmbeddingModel = ollama.AddModel(AIModels.EmbeddingGemma2_740M);

            web.WithReference(ollamaModel).WaitFor(ollamaModel)
                .WithReference(ollamaEmbeddingModel).WaitFor(ollamaEmbeddingModel)
                .WithEnvironment(EnvConfig.Keys.AI.ModelName, ollamaModel.Resource.ModelName)
                .WithEnvironment(EnvConfig.Keys.AI.EmbeddingModelName, ollamaEmbeddingModel.Resource.ModelName);
        }

        var frontend = builder.AddViteApp(ServiceNames.Frontend, "../../clients/sentium-portal")
            .WithPnpm()
            .WithReference(web).WaitFor(web)
            .WithEnvironment(EnvConfig.Keys.Frontend.ViteApiBase, web.GetEndpoint("https"))
            .WithEndpoint("http", e => e.Port = 5173);

        web.WithEnvironment(EnvConfig.Keys.FrontendOrigin, frontend.GetEndpoint("http"));
    }
}
