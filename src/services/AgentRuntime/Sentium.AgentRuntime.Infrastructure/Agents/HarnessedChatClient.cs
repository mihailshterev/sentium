using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.AI;
using Sentium.AgentRuntime.Core.Harness;
using Sentium.AgentRuntime.Core.Registry;
using Sentium.AgentRuntime.Infrastructure.Sentinel;

namespace Sentium.AgentRuntime.Infrastructure.Agents;

/// <summary>
/// Decorates an <see cref="IChatClient"/> by prepending a combined system prompt
/// (built-in governance policy + the user's own harness) to every request.
/// Instructions are resolved at call-time from <see cref="IRegistrySettingsService"/> scoped to the
/// acting user (<see cref="IPdpContextAccessor.UserId"/>) so changes take effect within the L1 cache
/// TTL without a service restart.
/// </summary>
public sealed class HarnessedChatClient(
    IChatClient innerClient,
    IRegistrySettingsService registrySettingsService,
    IPdpContextAccessor pdpContext) : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var modifiedMessages = await ApplyHarnessAsync(messages, cancellationToken);
        return await base.GetResponseAsync(modifiedMessages, options, cancellationToken);
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var modifiedMessages = await ApplyHarnessAsync(messages, cancellationToken);

        await foreach (var update in base.GetStreamingResponseAsync(modifiedMessages, options, cancellationToken))
        {
            yield return update;
        }
    }

    private async Task<List<ChatMessage>> ApplyHarnessAsync(IEnumerable<ChatMessage> messages, CancellationToken ct)
    {
        var settings = await registrySettingsService.GetAsync(pdpContext.UserId, ct);
        var harness = BuildHarness(settings);

        var messageList = messages.ToList();
        var systemMessage = messageList.FirstOrDefault(m => m.Role == ChatRole.System);

        if (systemMessage is not null)
        {
            var index = messageList.IndexOf(systemMessage);
            var combinedText = $"{harness}\n\n### AGENT SPECIFIC ROLE\n{systemMessage.Text}";
            messageList[index] = new ChatMessage(ChatRole.System, combinedText);
        }
        else
        {
            messageList.Insert(0, new ChatMessage(ChatRole.System, harness));
        }

        return messageList;
    }

    private static string BuildHarness(SettingsSnapshot settings)
    {
        var sb = new StringBuilder();

        if (settings.Harness.IsBuiltInHarnessEnabled)
        {
            sb.Append(UniversalSystemHarness.Policy);
        }

        if (!string.IsNullOrWhiteSpace(settings.Harness.UserHarnessPrompt))
        {
            if (sb.Length > 0)
            {
                sb.AppendLine().AppendLine();
            }

            sb.AppendLine("### USER-DEFINED BEHAVIOUR");
            sb.Append(settings.Harness.UserHarnessPrompt);
        }

        return sb.ToString().TrimEnd();
    }
}
