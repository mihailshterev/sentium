using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Constants;
using Sentium.Infrastructure.Security;
using Sentium.Infrastructure.Streaming;
using Sentium.Praxis.Features.Agents.Approvals;
using Sentium.Praxis.Features.Conversations;
using Sentium.Praxis.Features.Orchestration;
using Sentium.Praxis.Features.Workspaces;

namespace Sentium.Praxis.Features.Assistant;

/// <summary>
/// Accepts assistant turns and streams them. A turn runs on its own (see <see cref="AssistantTurnProcessor"/>), so the
/// client follows it on <c>turns/{turnId}/stream</c> and can drop and resume that stream at will; stopping a turn is an
/// explicit <c>turns/{turnId}/cancel</c>.
/// </summary>
[ApiController]
[Authorize]
[Route($"{PraxisRoutes.Prefix}/assistant")]
internal sealed class AssistantController(
    AssistantTurnQueue turnQueue,
    AssistantTurnRegistry turnRegistry,
    IStreamRelay streamRelay,
    IPendingApprovalStore approvalStore,
    ICurrentUser currentUser,
    IWorkspaceContextBuilder workspaceContext) : ControllerBase
{
    /// <summary>
    /// The user turn sent when files are attached without any text, so the turn is still recorded and titled.
    /// </summary>
    private const string AttachmentOnlyPrompt = "Please review the attached files.";

    private static readonly TimeSpan MaxStreamDuration = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Queues a chat turn and returns its id; follow it on <c>turns/{turnId}/stream</c> (thoughts, tool calls, text,
    /// approval requests). With a <see cref="ChatRequest.ConversationId"/> only the latest user message is taken from
    /// the request - prior turns are loaded from the persisted conversation. If a tool call needs approval, the turn
    /// ends with an approval request; resume it via <c>chat/approve</c>.
    /// </summary>
    [HttpPost("chat")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AssistantTurnAcceptedResponse>> Chat([FromBody] ChatRequest requestBody, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requestBody);

        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        IReadOnlyList<MessageAttachment> attachments = requestBody.FileIds is { Count: > 0 } fileIds
            ? await workspaceContext.ResolveAttachmentsAsync(userId, fileIds, ct)
            : [];

        var prompt = ResolvePrompt(requestBody.Messages, attachments);

        return Accepted(Enqueue(new AssistantTurn
        {
            TurnId = NewTurnId(),
            UserId = userId,
            CorrelationId = ResolveCorrelationId(),
            Model = requestBody.Model,
            ConversationId = requestBody.ConversationId,
            Prompt = prompt,
            OriginalUserPrompt = prompt ?? string.Empty,
            Messages = requestBody.Messages,
            Attachments = attachments,
            WorkspaceId = requestBody.WorkspaceId
        }));
    }

    /// <summary>
    /// Approves or rejects a pending tool call and queues the resumed run as a new turn. Only the user who owns the
    /// paused run can resume it.
    /// </summary>
    [HttpPost("chat/approve")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<AssistantTurnAcceptedResponse> ApproveToolCall([FromBody] ApproveToolCallRequest requestBody)
    {
        ArgumentNullException.ThrowIfNull(requestBody);

        if (currentUser.UserId is not { } userId
            || !approvalStore.TryTake(requestBody.RequestId, userId, out var pending)
            || pending is null)
        {
            return NotFound();
        }

        return Accepted(Enqueue(new AssistantTurn
        {
            TurnId = NewTurnId(),
            UserId = userId,
            CorrelationId = pending.CorrelationId,
            Model = pending.Model,
            ConversationId = pending.ConversationId,
            OriginalUserPrompt = pending.OriginalUserPrompt,
            Approval = new ApprovalDecision(pending, requestBody.Approved)
        }));
    }

    /// <summary>
    /// Streams a turn over SSE: the updates after <c>Last-Event-ID</c> or <c>?after=</c>, then live ones, ending with one
    /// terminal frame (<c>done</c>, <c>cancelled</c>, <c>error</c>, or <c>gone</c> for a turn that ended a while ago -
    /// its outcome is in the conversation).
    /// </summary>
    [HttpGet("turns/{turnId}/stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult StreamTurn(string turnId)
    {
        if (currentUser.UserId is not { } userId || !streamRelay.IsVisibleTo(turnId, userId))
        {
            return TypedResults.NotFound();
        }

        return RelayStreams.Create(
            streamRelay,
            turnId,
            RelayStreams.ResumePoint(Request),
            SseStreams.DefaultHeartbeatInterval,
            MaxStreamDuration,
            HttpContext.RequestAborted);
    }

    /// <summary>
    /// Stops the caller's turn (a no-op when it already ended). The partial answer is kept and the stream ends with a
    /// <c>cancelled</c> frame.
    /// </summary>
    [HttpPost("turns/{turnId}/cancel")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public IActionResult CancelTurn(string turnId)
    {
        if (currentUser.UserId is { } userId)
        {
            turnRegistry.CancelOwned(turnId, userId);
        }

        return Accepted();
    }

    private AssistantTurnAcceptedResponse Enqueue(AssistantTurn turn)
    {
        streamRelay.Claim(turn.TurnId, turn.UserId);
        turnQueue.Enqueue(turn);
        return new AssistantTurnAcceptedResponse(turn.TurnId);
    }

    private string ResolveCorrelationId()
    {
        return Request.Headers.TryGetValue(CommonHeaderNames.CorrelationId, out var header) ? header.ToString() : Guid.NewGuid().ToString();
    }

    private static string NewTurnId() => Guid.NewGuid().ToString("N");

    private static string? ResolvePrompt(IReadOnlyList<ChatMessage> messages, IReadOnlyList<MessageAttachment> attachments)
    {
        var content = messages.LastOrDefault(m => ChatRoles.IsUser(m.Role))?.Content;

        return attachments.Count > 0 && string.IsNullOrWhiteSpace(content) ? AttachmentOnlyPrompt : content;
    }
}
