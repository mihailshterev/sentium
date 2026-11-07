using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Sentium.Tests.Unit.Modules.Praxis.Support;

/// <summary>
/// Runs code inside a real Agent Framework run of an agent with the given name, so <see cref="AIAgent.CurrentRunContext"/>
/// is set exactly as it is for a tool the model calls.
/// </summary>
internal static class AgentRunScope
{
    public static async Task<T> RunAsAsync<T>(string agentName, Func<Task<T>> body)
    {
        using var model = new CallbackChatClient(async () => await body());
        var agent = new ChatClientAgent(model, name: agentName);

        await agent.RunAsync("run", await agent.CreateSessionAsync());

        return model.Result is T result ? result : throw new InvalidOperationException("The agent run did not reach the model.");
    }

    private sealed class CallbackChatClient(Func<Task<object?>> body) : IChatClient
    {
        public object? Result { get; private set; }

        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Result = await body();
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "done"));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }
    }
}
