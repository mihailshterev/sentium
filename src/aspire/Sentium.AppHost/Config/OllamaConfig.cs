namespace Sentium.AppHost.Config;

public static class OllamaConfig
{
    public const string ContextSizeKey = "OLLAMA_CONTEXT_LENGTH";
    public const string FlashAttentionKey = "OLLAMA_FLASH_ATTENTION";
    public const string CacheTypeKey = "OLLAMA_KV_CACHE_TYPE";
    public const string DebugKey = "OLLAMA_DEBUG";
    public const string ParallelRequestsKey = "OLLAMA_NUM_PARALLEL";

    public const string DefaultContextSize = "4096";
}
