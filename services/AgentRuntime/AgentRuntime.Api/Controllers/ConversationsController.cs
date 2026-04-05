using AgentRuntime.Core.Conversations;
using AgentRuntime.Core.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AgentRuntime.Api.Controllers;

[ApiController]
[Route("conversations")]
public sealed class ConversationsController(IConversationService conversationService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetConversations(CancellationToken ct)
    {
        var conversations = await conversationService.GetConversationsAsync(ct);
        return Ok(conversations);
    }

    [HttpGet("{conversationId:guid}")]
    public async Task<IActionResult> GetConversation(Guid conversationId, CancellationToken ct)
    {
        var conversation = await conversationService.GetConversationAsync(conversationId, ct);
        return Ok(conversation);
    }

    [HttpPost]
    public async Task<IActionResult> CreateConversation([FromBody] CreateConversationRequest request, CancellationToken ct)
    {
        var result = await conversationService.CreateConversationAsync(request, ct);
        return CreatedAtAction(nameof(GetConversation), new { conversationId = result.Id }, result);
    }

    [HttpDelete("{conversationId:guid}")]
    public async Task<IActionResult> DeleteConversation(Guid conversationId, CancellationToken ct)
    {
        await conversationService.DeleteConversationAsync(conversationId, ct);
        return NoContent();
    }
}
