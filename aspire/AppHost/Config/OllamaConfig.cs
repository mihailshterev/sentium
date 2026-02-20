namespace AppHost.Config;

public static class OllamaConfig
{
    public const string ContextSizeKey = "OLLAMA_NUM_CTX";
    public const string FlashAttentionKey = "OLLAMA_FLASH_ATTENTION";
    public const string CacheTypeKey = "OLLAMA_KV_CACHE_TYPE";
    public const string DebugKey = "OLLAMA_DEBUG";

    public const string DefaultContextSize = "2048";
}
