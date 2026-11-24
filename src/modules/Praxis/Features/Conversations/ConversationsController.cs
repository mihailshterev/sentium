using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Results;
using Sentium.Infrastructure.Results;

namespace Sentium.Praxis.Features.Conversations;

[ApiController]
[Authorize]
[Route($"{PraxisRoutes.Prefix}/conversations")]
internal sealed class ConversationsController(IConversationService conversationService) : ControllerBase
{
    /// <summary>
    /// Returns a page of conversations: pinned first, then by latest activity.
    /// </summary>
    /// <param name="page">1-based page number (default: 1).</param>
    /// <param name="pageSize">Number of items per page (default: 20, max: 100).</param>
    /// <param name="search">Optional text the conversation title or any of its messages must contain (case-insensitive).</param>
    /// <param name="archived">List the archived conversations instead of the active ones.</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ConversationSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ConversationSummary>>> GetConversations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationQuery.DefaultPageSize,
        [FromQuery] string? search = null,
        [FromQuery] bool archived = false,
        CancellationToken ct = default)
    {
        var conversations = await conversationService.GetConversationsAsync(page, pageSize, search, archived, ct);
        return Ok(conversations);
    }

    /// <summary>
    /// The conversation with its most recent messages; older ones come from <c>messages?before=</c>.
    /// </summary>
    /// <param name="messageLimit">How many of the newest messages to include (default 100, max 500).</param>
    [HttpGet("{conversationId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationResponse>> GetConversation(
        Guid conversationId,
        [FromQuery] int messageLimit = ConversationService.DefaultMessageLimit,
        CancellationToken ct = default)
    {
        var conversation = await conversationService.GetConversationAsync(conversationId, messageLimit, ct);
        return conversation is null ? NotFound() : Ok(conversation);
    }

    /// <summary>
    /// Messages older than <paramref name="before"/>, oldest first.
    /// </summary>
    /// <param name="before">The id of the oldest message already shown.</param>
    /// <param name="limit">How many messages to return (default 100, max 500).</param>
    [HttpGet("{conversationId:guid}/messages")]
    [ProducesResponseType(typeof(MessagePageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEarlierMessages(
        Guid conversationId,
        [FromQuery] Guid before,
        [FromQuery] int limit = ConversationService.DefaultMessageLimit,
        CancellationToken ct = default)
    {
        var page = await conversationService.GetMessagesBeforeAsync(conversationId, before, limit, ct);
        return page is null ? NotFound() : Ok(page);
    }

    [HttpPut("{conversationId:guid}/pinned")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPinned(Guid conversationId, [FromBody] SetConversationPinnedRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await conversationService.SetPinnedAsync(conversationId, request.Pinned, ct) ? NoContent() : NotFound();
    }

    /// <summary>
    /// Archives (and unpins) or unarchives a conversation. A new message unarchives it too.
    /// </summary>
    [HttpPut("{conversationId:guid}/archived")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetArchived(Guid conversationId, [FromBody] SetConversationArchivedRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await conversationService.SetArchivedAsync(conversationId, request.Archived, ct) ? NoContent() : NotFound();
    }

    [HttpPost]
    [ProducesResponseType(typeof(ConversationSummary), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateConversation([FromBody] CreateConversationRequest request, CancellationToken ct)
    {
        var result = await conversationService.CreateConversationAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetConversation), new { conversationId = result.Value!.Id }, result.Value)
            : result.ToActionResult(this);
    }

    /// <summary>
    /// Renames a conversation. A manual title is final: the conversation is no longer titled automatically.
    /// </summary>
    [HttpPut("{conversationId:guid}/title")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RenameConversation(Guid conversationId, [FromBody] UpdateTitleRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var renamed = await conversationService.RenameConversationAsync(conversationId, request.Title, ct);
        return renamed ? NoContent() : NotFound();
    }

    /// <summary>
    /// Binds the conversation to a workspace whose files every later turn can use, or unbinds it with a null id.
    /// </summary>
    [HttpPut("{conversationId:guid}/workspace")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetWorkspace(Guid conversationId, [FromBody] UpdateConversationWorkspaceRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await conversationService.SetWorkspaceAsync(conversationId, request.WorkspaceId, ct);
        return result.IsSuccess ? NoContent() : result.ToActionResult(this);
    }

    [HttpDelete("{conversationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteConversation(Guid conversationId, CancellationToken ct)
    {
        var deleted = await conversationService.DeleteConversationAsync(conversationId, ct);
        return deleted ? NoContent() : NotFound();
    }
}
