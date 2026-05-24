using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.AI;
using Sentium.AgentRuntime.Core.Harness;
using Sentium.AgentRuntime.Core.Settings;

namespace Sentium.AgentRuntime.Infrastructure.Agents;

/// <summary>
/// Decorates an <see cref="IChatClient"/> by prepending a combined system prompt
/// (built-in governance policy + user-defined harness) to every request.
/// Instructions are resolved at call-time from <see cref="ISystemSettingsService"/>
/// so changes take effect within the cache TTL (~30 s) without a service restart.
/// </summary>
public sealed class HarnessedChatClient(IChatClient innerClient, ISystemSettingsService systemSettingsService) : DelegatingChatClient(innerClient)
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
        var settings = await systemSettingsService.GetAsync(ct);
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

    private static string BuildHarness(SystemSettingsDto settings)
    {
        var sb = new StringBuilder();

        if (settings.IsBuiltInHarnessEnabled)
        {
            sb.Append(UniversalSystemHarness.Policy);
        }

        if (!string.IsNullOrWhiteSpace(settings.UserHarnessPrompt))
        {
            if (sb.Length > 0)
            {
                sb.AppendLine().AppendLine();
            }

            sb.AppendLine("### USER-DEFINED GLOBAL BEHAVIOUR");
            sb.Append(settings.UserHarnessPrompt);
        }

        return sb.ToString().TrimEnd();
    }
}
