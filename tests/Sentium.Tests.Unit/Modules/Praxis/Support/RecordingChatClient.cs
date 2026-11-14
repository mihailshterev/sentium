using Microsoft.Extensions.AI;

namespace Sentium.Tests.Unit.Modules.Praxis.Support;

/// <summary>
/// A model stand-in: records every request and replies with queued responses, then "ok".
/// </summary>
internal sealed class RecordingChatClient : IChatClient
{
    private readonly Queue<ChatResponse> _scripted = new();

    public List<(List<ChatMessage> Messages, ChatOptions? Options)> Requests { get; } = [];

    public void Script(ChatResponse response) => _scripted.Enqueue(response);

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Requests.Add(([.. messages], options?.Clone()));
        return Task.FromResult(_scripted.TryDequeue(out var next) ? next : new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var update in response.ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }
}
