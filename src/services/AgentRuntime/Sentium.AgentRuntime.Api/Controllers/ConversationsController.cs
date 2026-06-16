using Sentium.AgentRuntime.Core.Conversations;
using Sentium.AgentRuntime.Core.Dtos;
using Sentium.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sentium.AgentRuntime.Api.Controllers;

/// <summary>
/// Controller for managing assistant conversations.
/// </summary>
[ApiController]
[Authorize]
[Route("conversations")]
public sealed class ConversationsController(IConversationService conversationService) : ControllerBase
{
    /// <summary>
    /// Returns a page of conversations (newest first).
    /// </summary>
    /// <param name="page">1-based page number (default: 1).</param>
    /// <param name="pageSize">Number of items per page (default: 20, max: 100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paginated list of conversations.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ConversationSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ConversationSummary>>> GetConversations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationQuery.DefaultPageSize,
        CancellationToken ct = default)
    {
        var conversations = await conversationService.GetConversationsAsync(page, pageSize, ct);
        return Ok(conversations);
    }

    /// <summary>
    /// Returns details of a specific conversation by its ID.
    /// </summary>
    /// <param name="conversationId">The ID of the conversation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Details of the conversation.</returns>
    [HttpGet("{conversationId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationResponse>> GetConversation(Guid conversationId, CancellationToken ct)
    {
        var conversation = await conversationService.GetConversationAsync(conversationId, ct);
        return conversation is null ? NotFound() : Ok(conversation);
    }

    /// <summary>
    /// Creates a new conversation.
    /// </summary>
    /// <param name="request">The request containing conversation details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created conversation.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<ConversationSummary>> CreateConversation([FromBody] CreateConversationRequest request, CancellationToken ct)
    {
        var result = await conversationService.CreateConversationAsync(request, ct);
        return CreatedAtAction(nameof(GetConversation), new { conversationId = result.Id }, result);
    }

    /// <summary>
    /// Deletes a specific conversation by its ID.
    /// </summary>
    /// <param name="conversationId">The ID of the conversation to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content.</returns>
    [HttpDelete("{conversationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteConversation(Guid conversationId, CancellationToken ct)
    {
        var deleted = await conversationService.DeleteConversationAsync(conversationId, ct);
        return deleted ? NoContent() : NotFound();
    }
}
