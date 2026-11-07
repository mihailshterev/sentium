namespace Sentium.AppHost.Config;

/// <summary>
/// llama.cpp server containers: the <c>llm</c> router (chat models from <c>infra/llm/models.ini</c>) and the
/// <c>embeddings</c> server, plus the one-shot model fetcher. Mirrors <c>docker-compose.yml</c>.
/// </summary>
public static class LlamaConfig
{
    public const string Registry = "ghcr.io";
    public const string Image = "ggml-org/llama.cpp";

    // Build b11804 (2026-10-22), pinned by digest. Upgrade the tags and digests together, then re-check tool
    // calling, reasoning output and embeddings against the new build.
    public const string GpuTag = "server-cuda-b11804";
    public const string GpuDigest = "490d8cd8000203a3ab8fdfce14ef931e2aa85affd4dd4447bcc75f299f00fd9a";
    public const string CpuTag = "server-b11804";
    public const string CpuDigest = "8c74db29f1a2bef3f0a331b52c4d6214601dfceed4bee0b94cc48b3b23ff257d";

    public const string InitImage = "curlimages/curl";
    public const string InitTag = "8.22.0";
    public const string InitDigest = "58adaa4e8dca9c988bae2aba4ab3434a0bb2da16bbe3f92dec39ec7785166777";

    /// <summary>
    /// Set to <c>false</c> (e.g. <c>Llm__UseGpu=false</c> or user secrets) on hosts without an NVIDIA GPU.
    /// </summary>
    public const string UseGpuKey = "Llm:UseGpu";

    /// <summary>
    /// The port the llama.cpp server images listen on (they set <c>LLAMA_ARG_PORT=8080</c>).
    /// </summary>
    public const int ContainerPort = 8080;
    public const int LlmHostPort = 8090;
    public const int EmbeddingsHostPort = 8091;

    /// <summary>
    /// Named volume holding the verified model files. Shared with Docker Compose, so models download once.
    /// </summary>
    public const string ModelsVolume = "sentium-llm-models";
    public const string ModelsPath = "/models";

    /// <summary>
    /// <c>infra/llm</c> (lock file, fetch script, presets), relative to the AppHost project directory.
    /// </summary>
    public const string InfraSource = "../../../infra/llm";
    public const string InfraPath = "/infra";

    public const string EmbeddingModelFile = "embeddinggemma-2-Q8_0.gguf";
}
