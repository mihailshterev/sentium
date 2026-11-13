using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Results;
using Sentium.Infrastructure.Results;
using Sentium.Praxis.Features.Conversations;

namespace Sentium.Praxis.Features.Conversations;

[ApiController]
[Authorize]
[Route($"{PraxisRoutes.Prefix}/conversations")]
public sealed class ConversationsController(IConversationService conversationService) : ControllerBase
{
    /// <summary>
    /// Returns a page of conversations (newest first).
    /// </summary>
    /// <param name="page">1-based page number (default: 1).</param>
    /// <param name="pageSize">Number of items per page (default: 20, max: 100).</param>
    /// <param name="search">Optional text the conversation title must contain (case-insensitive).</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ConversationSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ConversationSummary>>> GetConversations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationQuery.DefaultPageSize,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var conversations = await conversationService.GetConversationsAsync(page, pageSize, search, ct);
        return Ok(conversations);
    }

    [HttpGet("{conversationId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationResponse>> GetConversation(Guid conversationId, CancellationToken ct)
    {
        var conversation = await conversationService.GetConversationAsync(conversationId, ct);
        return conversation is null ? NotFound() : Ok(conversation);
    }

    /// <param name="request">The request containing conversation details.</param>
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

    [HttpDelete("{conversationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteConversation(Guid conversationId, CancellationToken ct)
    {
        var deleted = await conversationService.DeleteConversationAsync(conversationId, ct);
        return deleted ? NoContent() : NotFound();
    }
}
