namespace Sentium.Infrastructure.Inference;

/// <summary>
/// Which inference server a named HttpClient talks to.
/// </summary>
public enum InferenceEndpoint
{
    Chat,
    Embedding
}
