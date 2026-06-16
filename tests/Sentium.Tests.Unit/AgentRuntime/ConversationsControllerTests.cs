using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Sentium.AgentRuntime.Api.Controllers;
using Sentium.AgentRuntime.Core.Conversations;
using Sentium.AgentRuntime.Core.Dtos;
using Sentium.Shared.Results;
using Xunit;

namespace Sentium.Tests.Unit.AgentRuntime;

public sealed class ConversationsControllerTests
{
    private readonly IConversationService _service = Substitute.For<IConversationService>();
    private readonly ConversationsController _controller;

    public ConversationsControllerTests()
    {
        _controller = new ConversationsController(_service);
    }

    [Fact]
    public async Task GetConversations_ReturnsOkWithPagedResponse()
    {
        var conversations = new List<ConversationSummary>
        {
            new(Guid.NewGuid(), "Chat 1", "gemma3:1b", DateTime.UtcNow)
        };
        var paged = PagedResponse<ConversationSummary>.Create(conversations, 1, 1, 20);
        _service.GetConversationsAsync(1, 20, Arg.Any<CancellationToken>()).Returns(paged);

        var result = await _controller.GetConversations(1, 20, TestContext.Current.CancellationToken);

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().Be(paged);
    }

    [Fact]
    public async Task GetConversation_ExistingId_ReturnsOk()
    {
        var id = Guid.NewGuid();
        var expected = new ConversationResponse(id, "Chat", "gemma3:1b", DateTime.UtcNow, []);
        _service.GetConversationAsync(id, Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _controller.GetConversation(id, TestContext.Current.CancellationToken);

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().Be(expected);
    }

    [Fact]
    public async Task CreateConversation_ValidRequest_ReturnsCreated()
    {
        var request = new CreateConversationRequest("New Chat", "gemma3:1b");
        var created = new ConversationSummary(Guid.NewGuid(), "New Chat", "gemma3:1b", DateTime.UtcNow);
        _service.CreateConversationAsync(request, Arg.Any<CancellationToken>()).Returns(created);

        var result = await _controller.CreateConversation(request, TestContext.Current.CancellationToken);

        result.Result.Should().BeOfType<CreatedAtActionResult>()
            .Which.Value.Should().Be(created);
    }

    [Fact]
    public async Task DeleteConversation_ExistingId_ReturnsNoContent()
    {
        var id = Guid.NewGuid();
        _service.DeleteConversationAsync(id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _controller.DeleteConversation(id, TestContext.Current.CancellationToken);

        result.Should().BeOfType<NoContentResult>();
    }
}
