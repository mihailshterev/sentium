namespace Sentium.Praxis.Features.Models;

/// <summary>
/// Resolves how many tokens one request to a chat model may use - llama-server's per-slot context
/// (<c>ctx-size / parallel</c> in <c>infra/llm/models.ini</c>). Agents size their context compaction from it.
/// The window is server configuration, so it is read from the server, never sent with a request.
/// </summary>
internal interface IModelContextWindowProvider
{
    ValueTask<int> GetContextWindowAsync(string modelId, CancellationToken ct = default);
}
