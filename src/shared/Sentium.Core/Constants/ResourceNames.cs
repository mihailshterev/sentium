namespace Sentium.Core.Constants;

public static class ResourceNames
{
    /// <summary>llama-server in router mode, serving the chat models.</summary>
    public const string Llm = "llm";

    /// <summary>llama-server serving the embedding model.</summary>
    public const string Embeddings = "embeddings";

    /// <summary>One-shot container that downloads and verifies the model files.</summary>
    public const string ModelsInit = "models-init";
    public const string Postgres = "postgres";
    public const string SentiumDb = "sentiumdb";
    public const string Redis = "redis";
    public const string Storage = "storage";
    public const string WorkspaceBlobs = "workspace-storage";
}
