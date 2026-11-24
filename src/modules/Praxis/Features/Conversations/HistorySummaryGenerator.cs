using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace Sentium.Praxis.Features.Conversations;

/// <summary>
/// Folds messages into a conversation's running summary with the base (non-harnessed) chat client: one completion,
/// outside the agent governance pipeline, like titles.
/// </summary>
internal sealed partial class HistorySummaryGenerator(IChatClient chatClient, ILogger<HistorySummaryGenerator> logger)
{
    /// <summary>
    /// The longest stored summary, in characters; a model that runs on is cut here.
    /// </summary>
    public const int MaxSummaryLength = 4_000;

    /// <summary>
    /// How much of one message the summary sees; the start of a long answer carries its point.
    /// </summary>
    public const int MaxMessageLength = 1_500;

    private const int MaxOutputTokens = 700;

    private const string SystemPrompt = """
        You keep a running summary of a long conversation between a user and an AI assistant, so the assistant can
        continue it after the older messages are gone.
        Update the current summary with the new messages and reply with ONLY the updated summary.
        Rules:
        - Keep facts, names, numbers, decisions, preferences, constraints, open questions and promises made.
        - Drop greetings, small talk and anything later messages replaced or corrected.
        - Write short bullet points in the language of the conversation, at most 300 words in total.
        - Do not answer or continue the conversation.
        """;

    /// <returns>The updated summary, or <see langword="null"/> when the model failed or returned nothing.</returns>
    public async Task<string?> SummarizeAsync(string? currentSummary, IReadOnlyList<MessageResponse> messages, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var request = new List<ChatMessage>
        {
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User, BuildInput(currentSummary, messages)),
        };

        try
        {
            var response = await chatClient.GetResponseAsync(request, new ChatOptions { Temperature = 0.2f, MaxOutputTokens = MaxOutputTokens }, ct);
            var text = ThinkBlock().Replace(response.Text, string.Empty).Trim();

            return text.Length == 0 ? null : text.Length <= MaxSummaryLength ? text : text[..MaxSummaryLength];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The conversation summary could not be updated; it is retried after the next turn.");
            return null;
        }
    }

    public static int CostOf(MessageResponse message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return Math.Min(message.Content.Length, MaxMessageLength);
    }

    private static string BuildInput(string? currentSummary, IReadOnlyList<MessageResponse> messages)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Current summary:");
        sb.AppendLine(string.IsNullOrWhiteSpace(currentSummary) ? "(none yet)" : currentSummary.Trim());
        sb.AppendLine();
        sb.AppendLine("New messages:");

        foreach (var message in messages)
        {
            var speaker = string.Equals(message.Role, ChatRole.User.ToString(), StringComparison.OrdinalIgnoreCase) ? "User" : "Assistant";
            var content = message.Content.ReplaceLineEndings(" ").Trim();
            if (content.Length > MaxMessageLength)
            {
                content = string.Concat(content.AsSpan(0, MaxMessageLength), "…");
            }

            sb.AppendLine(CultureInfo.InvariantCulture, $"{speaker}: {content}");
        }

        return sb.ToString();
    }

    [GeneratedRegex(@"<think>.*?</think>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ThinkBlock();
}
