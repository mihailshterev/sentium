using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using ChatCompletion = OpenAI.Chat.ChatCompletion;
using StreamingChatCompletionUpdate = OpenAI.Chat.StreamingChatCompletionUpdate;

namespace Sentium.Infrastructure.Inference;

/// <summary>
/// Surfaces a model's thinking from the non-standard <c>reasoning</c> field of an OpenAI-compatible chat
/// completion (Ollama's <c>/v1</c>, OpenRouter, newer vLLM) as <see cref="TextReasoningContent"/>, which the
/// agent streams turn into "thought" updates.
/// <para>
/// Microsoft.Extensions.AI's OpenAI adapter already maps the older <c>reasoning_content</c> field (DeepSeek,
/// llama.cpp's llama-server) but not <c>reasoning</c>. This reads the field through the same raw-JSON
/// <c>Patch</c> accessor the adapter uses, and only when the adapter produced no reasoning itself, so a
/// server that sends <c>reasoning_content</c> never yields duplicates.
/// </para>
/// </summary>
#pragma warning disable SCME0001 // JsonPatch is experimental; it is the adapter's own mechanism for reasoning_content.
internal sealed class ReasoningFieldChatClient(IChatClient innerClient) : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);

        if (response.RawRepresentation is ChatCompletion completion
            && response.Messages.Count > 0
            && !response.Messages.Any(m => m.Contents.OfType<TextReasoningContent>().Any())
            && TryGetReasoning(completion) is { } reasoning)
        {
            response.Messages[^1].Contents.Insert(0, new TextReasoningContent(reasoning) { RawRepresentation = completion });
        }

        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken).ConfigureAwait(false))
        {
            if (update.RawRepresentation is StreamingChatCompletionUpdate chunk
                && !update.Contents.OfType<TextReasoningContent>().Any()
                && TryGetReasoning(chunk) is { } reasoning)
            {
                update.Contents.Insert(0, new TextReasoningContent(reasoning) { RawRepresentation = chunk });
            }

            yield return update;
        }
    }

    // JsonPatch resolves "$.choices[0]" by indexing the typed choices list, which throws on a chunk without
    // choices, such as the trailing usage-only chunk ("choices": []) that Ollama and llama-server stream.
    // That chunk carries no reasoning, so it is treated as absent.

    private static string? TryGetReasoning(StreamingChatCompletionUpdate chunk)
    {
        try
        {
            return chunk.Patch.TryGetValue("$.choices[0].delta.reasoning"u8, out string? value) && !string.IsNullOrEmpty(value) ? value : null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static string? TryGetReasoning(ChatCompletion completion)
    {
        try
        {
            return completion.Patch.TryGetValue("$.choices[0].message.reasoning"u8, out string? value) && !string.IsNullOrEmpty(value) ? value : null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
#pragma warning restore SCME0001
