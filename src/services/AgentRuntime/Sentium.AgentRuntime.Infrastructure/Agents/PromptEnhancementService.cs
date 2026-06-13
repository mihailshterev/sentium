using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Sentium.AgentRuntime.Core.Agents;

namespace Sentium.AgentRuntime.Infrastructure.Agents;

/// <summary>
/// Enhances prompts using the base (non-harnessed) chat client, so the
/// optimization pass stays lightweight and independent of the agent governance pipeline.
/// </summary>
public sealed class PromptEnhancementService(IChatClient chatClient, ILogger<PromptEnhancementService> logger) : IPromptEnhancementService
{
    private const string SystemPrompt = """
        You are a prompt optimizer for a smaller local language model. Rewrite the user's request into a single, clear, specific, well-structured prompt that will produce the best result.
        Rules:
        - Preserve the original intent exactly. Do NOT answer the request, add new requirements, or invent details.
        - Keep concrete details verbatim: IDs, file names, code, quoted text, and bracketed context tags like [Workspace: name | ID: ...] or [File: name | ID: ...].
        - Make implicit goals explicit and state the desired output format when it is obvious.
        - Keep it concise. Output ONLY the rewritten prompt - no preamble, quotes, labels, or explanation.
        """;

    public async Task<string> EnhanceAsync(string prompt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return prompt;
        }

        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, SystemPrompt),
                new(ChatRole.User, prompt)
            };

            var response = await chatClient.GetResponseAsync(messages, new ChatOptions { Temperature = 0.3f }, ct);
            var enhanced = response.Text;

            return string.IsNullOrWhiteSpace(enhanced) ? prompt : enhanced.Trim();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Prompt enhancement failed; falling back to the original prompt");
            return prompt;
        }
    }
}
