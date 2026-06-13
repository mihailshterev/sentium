using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.AI;
using Sentium.AgentRuntime.Core.Learnings;
using Sentium.AgentRuntime.Infrastructure.Sentinel;

namespace Sentium.AgentRuntime.Infrastructure.Agents;

/// <summary>
/// Decorates an <see cref="IChatClient"/> by semantically recalling prior agent learnings relevant to the
/// latest user message and injecting them into the system prompt. This closes the self-improvement loop:
/// insights captured via <c>capture_agent_learning</c> are automatically fed back into every agent run.
/// <para>
/// Applied as the outermost decorator (above <see cref="HarnessedChatClient"/>) so it runs once per agent
/// turn rather than per tool round. Recall is best-effort - failures leave the messages untouched.
/// </para>
/// </summary>
public sealed class LearningAugmentedChatClient(
    IChatClient innerClient,
    IAgentLearningService learningService,
    IPdpContextAccessor pdpContext) : DelegatingChatClient(innerClient)
{
    private const int MaxLearnings = 4;

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var modifiedMessages = await ApplyLearningsAsync(messages, cancellationToken);
        return await base.GetResponseAsync(modifiedMessages, options, cancellationToken);
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var modifiedMessages = await ApplyLearningsAsync(messages, cancellationToken);

        await foreach (var update in base.GetStreamingResponseAsync(modifiedMessages, options, cancellationToken))
        {
            yield return update;
        }
    }

    private async Task<IEnumerable<ChatMessage>> ApplyLearningsAsync(IEnumerable<ChatMessage> messages, CancellationToken ct)
    {
        var messageList = messages.ToList();

        var latestUserText = messageList.LastOrDefault(m => m.Role == ChatRole.User)?.Text;
        if (string.IsNullOrWhiteSpace(latestUserText))
        {
            return messageList;
        }

        var learnings = await learningService.RecallRelevantAsync(latestUserText, pdpContext.UserId, MaxLearnings, ct);
        if (learnings.Count == 0)
        {
            return messageList;
        }

        var block = BuildLearningsBlock(learnings);

        var systemMessage = messageList.FirstOrDefault(m => m.Role == ChatRole.System);
        if (systemMessage is not null)
        {
            var index = messageList.IndexOf(systemMessage);
            messageList[index] = new ChatMessage(ChatRole.System, $"{systemMessage.Text}\n\n{block}");
        }
        else
        {
            messageList.Insert(0, new ChatMessage(ChatRole.System, block));
        }

        return messageList;
    }

    private static string BuildLearningsBlock(IReadOnlyList<RecalledLearning> learnings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("### RELEVANT PRIOR LEARNINGS");
        sb.AppendLine("Insights captured from past runs. Consult them before acting and reuse what applies; expand with the `recall_learnings` tool if you need more.");

        foreach (var learning in learnings)
        {
            sb.Append("- ").AppendLine(learning.Content.ReplaceLineEndings(" ").Trim());
        }

        return sb.ToString().TrimEnd();
    }
}
