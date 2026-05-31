using Sentium.AgentRuntime.Core.Conversations;
using Sentium.AgentRuntime.Core.Dtos;
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
    /// Returns a list of conversations. Optionally filter by agent name or session ID.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of conversations.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConversationSummary>>> GetConversations(CancellationToken ct)
    {
        var conversations = await conversationService.GetConversationsAsync(ct);
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
    public async Task<ActionResult<ConversationResponse>> GetConversation(Guid conversationId, CancellationToken ct)
    {
        var conversation = await conversationService.GetConversationAsync(conversationId, ct);
        return Ok(conversation);
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
    public async Task<IActionResult> DeleteConversation(Guid conversationId, CancellationToken ct)
    {
        await conversationService.DeleteConversationAsync(conversationId, ct);
        return NoContent();
    }
}
