using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Sentium.AgentRuntime.Infrastructure.Agents;

public sealed class HarnessedChatClient(IChatClient innerClient, string globalInstructions) : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var modifiedMessages = ApplyHarness(messages);
        return await base.GetResponseAsync(modifiedMessages, options, cancellationToken);
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var modifiedMessages = ApplyHarness(messages);

        await foreach (var update in base.GetStreamingResponseAsync(modifiedMessages, options, cancellationToken))
        {
            yield return update;
        }
    }

    private List<ChatMessage> ApplyHarness(IEnumerable<ChatMessage> messages)
    {
        var messageList = messages.ToList();
        var systemMessage = messageList.FirstOrDefault(m => m.Role == ChatRole.System);

        if (systemMessage != null)
        {
            var index = messageList.IndexOf(systemMessage);

            var combinedText = $"{globalInstructions}\n\n### AGENT SPECIFIC ROLE\n{systemMessage.Text}";
            messageList[index] = new ChatMessage(ChatRole.System, combinedText);
        }
        else
        {
            messageList.Insert(0, new ChatMessage(ChatRole.System, globalInstructions));
        }

        return messageList;
    }
}
