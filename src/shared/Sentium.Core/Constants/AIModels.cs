namespace Sentium.Core.Constants;

/// <summary>
/// Model ids served by the inference servers. Chat model ids are the section names of the router presets
/// in <c>infra/llm/models.ini</c>; the files behind them are pinned in <c>infra/llm/models.lock</c>.
/// </summary>
public static class AIModels
{
    public const string Gemma4_E4B = "gemma-4-e4b";

    // Embedding model for the RAG pipeline, served by its own llama-server instance.
    // Produces 768-dimensional vectors - must match CodexDbContext.EmbeddingDimensions (vector(768) column).
    // Codex re-embeds stored chunks automatically when this id changes.
    public const string EmbeddingGemma2 = "embeddinggemma-2";
}
