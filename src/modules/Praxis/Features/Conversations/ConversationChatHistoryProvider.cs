using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Features.Sentinel;

namespace Sentium.Praxis.Features.Conversations;

/// <summary>
/// Agent Framework chat history backed by the persisted conversation (<c>Conversations</c>/<c>Messages</c> tables), so
/// the server - not the client - is the source of truth for what the model sees.
/// </summary>
/// <remarks>
/// <para>The model's history is the persisted text transcript (compact turns that suit small context windows) plus the
/// <em>in-flight turn</em>: when a run pauses on a tool-approval request, its raw messages (the user's question and the
/// assistant's approval request) are kept in the <see cref="AgentSession"/> rather than the transcript, so the resumed
/// run - whose only input is the approval response - sees exactly what it needs and the framework can bind the response
/// to its request. When the turn completes, a single merged assistant message is written to the transcript.</para>
/// <para>The user's turn is written as soon as a turn starts finishing (completed, paused, or failed), so the transcript
/// always shows what was asked. Trailing unanswered user turns are skipped when loading, so a retry or a resume never
/// hands the model two consecutive user messages. Ownership is checked against the acting user on every load and store.</para>
/// </remarks>
public sealed class ConversationChatHistoryProvider : ChatHistoryProvider
{
    /// <summary>
    /// <see cref="ChatMessage.AdditionalProperties"/> key carrying the user's original text when the prompt sent to the
    /// model was rewritten by prompt enhancement. Persisted as the message content, with the sent text as the enhanced prompt.
    /// </summary>
    public const string OriginalPromptProperty = "sentium.originalPrompt";

    private const int MaxHistoryMessages = 20;

    private readonly ProviderSessionState<State> sessionState;
    private readonly IConversationRepository repository;
    private readonly IConversationService conversations;
    private readonly IPdpContextAccessor runContext;
    private readonly ILogger<ConversationChatHistoryProvider> logger;

    public ConversationChatHistoryProvider(
        IConversationRepository repository,
        IConversationService conversations,
        IPdpContextAccessor runContext,
        ILogger<ConversationChatHistoryProvider> logger)
        : base(null, null, null)
    {
        // AIJsonUtilities options serialize ChatMessage/AIContent polymorphically (the in-flight turn holds them).
        sessionState = new ProviderSessionState<State>(_ => new State(), nameof(ConversationChatHistoryProvider), AIJsonUtilities.DefaultOptions);
        this.repository = repository;
        this.conversations = conversations;
        this.runContext = runContext;
        this.logger = logger;
    }

    /// <summary>
    /// Binds <paramref name="session"/> to a persisted conversation.
    /// </summary>
    public void Attach(AgentSession session, Guid conversationId) =>
        sessionState.SaveState(session, new State { ConversationId = conversationId });

    /// <summary>
    /// Label shown for a tool call in the live stream and the persisted transcript.
    /// </summary>
    public static string ToolCallLabel(string toolName) => $"Calling {toolName}...";

    /// <summary>
    /// Records the partial answer of a run the client cancelled mid-stream (the framework does not store responses of
    /// cancelled runs; the user's turn itself is recorded by <see cref="InvokedCoreAsync"/>).
    /// </summary>
    public async Task PersistPartialResponseAsync(AgentSession session, string text, string? thought, IReadOnlyList<string>? toolCalls)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(text) || await ResolveOwnedConversationAsync(session, CancellationToken.None) is not { } conversationId)
        {
            return;
        }

        await conversations.AddMessageAsync(conversationId, ChatRole.Assistant.ToString(), text, thought: thought, toolCalls: toolCalls, ct: CancellationToken.None);
    }

    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var state = sessionState.GetOrInitializeState(context.Session);
        if (state.ConversationId is not { } conversationId || runContext.UserId is not { } userId)
        {
            return [];
        }

        var rows = await repository.GetRecentMessagesAsync(conversationId, userId, MaxHistoryMessages, cancellationToken);

        var history = rows
            .Select(row => IsUser(row.Role)
                ? new ChatMessage(ChatRole.User, row.EnhancedPrompt ?? row.Content)
                : new ChatMessage(ChatRole.Assistant, row.Content))
            .Where(m => !string.IsNullOrWhiteSpace(m.Text))
            .ToList();

        // The newest user turn is supplied by this run's input or, when resuming, by the in-flight turn - never twice.
        var resuming = state.PendingTurn is { Count: > 0 };
        if (resuming || context.RequestMessages.Any(IsUserText))
        {
            var end = history.Count;
            while (end > 0 && history[end - 1].Role == ChatRole.User)
            {
                end--;
            }

            history.RemoveRange(end, history.Count - end);
        }

        return resuming ? [.. history, .. state.PendingTurn!] : history;
    }

    protected override async ValueTask StoreChatHistoryAsync(InvokedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var state = sessionState.GetOrInitializeState(context.Session);
        if (await ResolveOwnedConversationAsync(context.Session, cancellationToken) is not { } conversationId)
        {
            return;
        }

        var external = context.RequestMessages.Where(IsExternal).ToList();
        var responses = context.ResponseMessages?.ToList() ?? [];
        var resuming = state.PendingTurn is { Count: > 0 };

        if (!resuming)
        {
            await PersistUserTurnsAsync(conversationId, external, cancellationToken);
        }

        if (responses.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().Any())
        {
            // Paused on approval: keep the raw turn in the session until it completes.
            state.PendingTurn = [.. state.PendingTurn ?? [], .. external, .. responses];
            sessionState.SaveState(context.Session, state);
            return;
        }

        var (text, thought, toolCalls) = Summarize(resuming ? [.. state.PendingTurn!, .. responses] : responses);
        if (text.Length > 0)
        {
            await conversations.AddMessageAsync(
                conversationId,
                ChatRole.Assistant.ToString(),
                text,
                thought: thought.Length > 0 ? thought : null,
                toolCalls: toolCalls.Count > 0 ? toolCalls : null,
                ct: cancellationToken);
        }

        if (resuming)
        {
            state.PendingTurn = null;
            sessionState.SaveState(context.Session, state);
        }
    }

    protected override async ValueTask InvokedCoreAsync(InvokedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.InvokeException is null)
        {
            await base.InvokedCoreAsync(context, cancellationToken);
            return;
        }

        // Failed or cancelled run: record what the user asked (a resumed turn already recorded it) and drop the in-flight turn.
        try
        {
            var state = sessionState.GetOrInitializeState(context.Session);
            if (state.PendingTurn is null && await ResolveOwnedConversationAsync(context.Session, CancellationToken.None) is { } conversationId)
            {
                await PersistUserTurnsAsync(conversationId, context.RequestMessages.Where(IsExternal), CancellationToken.None);
            }

            state.PendingTurn = null;
            sessionState.SaveState(context.Session, state);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to record the user's turn after a failed agent run.");
        }
    }

    /// <summary>
    /// Flattens a turn into the transcript shape the UI renders: answer text, reasoning, and tool-call labels.
    /// </summary>
    internal static (string Text, string Thought, List<string> ToolCalls) Summarize(IEnumerable<ChatMessage> messages)
    {
        var text = new StringBuilder();
        var thought = new StringBuilder();
        var toolCalls = new List<string>();

        foreach (var message in messages.Where(m => m.Role == ChatRole.Assistant))
        {
            foreach (var content in message.Contents)
            {
                switch (content)
                {
                    case TextContent t:
                        text.Append(t.Text);
                        break;
                    case TextReasoningContent r:
                        thought.Append(r.Text);
                        break;
                    case FunctionCallContent call:
                        toolCalls.Add(ToolCallLabel(call.Name));
                        break;
                }
            }
        }

        return (text.ToString(), thought.ToString(), toolCalls);
    }

    private async Task PersistUserTurnsAsync(Guid conversationId, IEnumerable<ChatMessage> messages, CancellationToken ct)
    {
        foreach (var message in messages.Where(IsUserText))
        {
            var original = message.AdditionalProperties?.TryGetValue(OriginalPromptProperty, out var value) == true ? value as string : null;

            await conversations.AddMessageAsync(
                conversationId,
                ChatRole.User.ToString(),
                original ?? message.Text,
                enhancedPrompt: original is null ? null : message.Text,
                ct: ct);
        }
    }

    private async Task<Guid?> ResolveOwnedConversationAsync(AgentSession? session, CancellationToken ct)
    {
        if (sessionState.GetOrInitializeState(session).ConversationId is not { } conversationId || runContext.UserId is not { } userId)
        {
            return null;
        }

        if (await repository.IsOwnedByAsync(conversationId, userId, ct))
        {
            return conversationId;
        }

        logger.LogWarning("Refusing to persist chat history into conversation {ConversationId}: not owned by the acting user.", conversationId);
        return null;
    }

    private static bool IsExternal(ChatMessage message) =>
        message.GetAgentRequestMessageSourceType() == AgentRequestMessageSourceType.External;

    private static bool IsUserText(ChatMessage message) =>
        message.Role == ChatRole.User && !string.IsNullOrWhiteSpace(message.Text);

    private static bool IsUser(string role) => string.Equals(role, ChatRole.User.ToString(), StringComparison.OrdinalIgnoreCase);

    private sealed class State
    {
        public Guid? ConversationId { get; set; }

        /// <summary>Raw messages of a turn paused on tool approval, replayed into the resumed run.</summary>
        public List<ChatMessage>? PendingTurn { get; set; }
    }
}
