namespace AppHost.Constants;

public static class AIModels
{
    public const string Gemma3_1B = "gemma3:1b";
    public const string Qwen3_4B = "qwen3:4b";
    public const string Qwen3_8B = "qwen3:8b";
    public const string Qwen3_8B_Q4KM = "qwen3:8b-q4_K_M";
    public const string Qwen3_5_9B_Q4KM = "qwen3.5:9b-q4_K_M";
    public const string Gemma4 = "gemma4:e4b";

    // Embedding model for the RAG pipeline.
    // Produces 768-dimensional vectors — must match RagOptions.VectorSize.
    public const string NomicEmbedText = "nomic-embed-text";
}
