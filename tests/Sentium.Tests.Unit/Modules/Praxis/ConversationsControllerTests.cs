using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Sentium.Praxis.Controllers;
using Sentium.Praxis.Features.Conversations;
using Sentium.Praxis.Dtos;
using Sentium.Core.Results;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis;

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
        _service.GetConversationsAsync(1, 20, null, Arg.Any<CancellationToken>()).Returns(paged);

        var result = await _controller.GetConversations(1, 20, null, TestContext.Current.CancellationToken);

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
        _service.CreateConversationAsync(request, Arg.Any<CancellationToken>()).Returns(Result<ConversationSummary>.Success(created));

        var result = await _controller.CreateConversation(request, TestContext.Current.CancellationToken);

        result.Should().BeOfType<CreatedAtActionResult>()
            .Which.Value.Should().Be(created);
    }

    [Fact]
    public async Task CreateConversation_UnknownAgent_ReturnsBadRequest()
    {
        var request = new CreateConversationRequest(null, "gemma3:1b", "Nobody");
        _service.CreateConversationAsync(request, Arg.Any<CancellationToken>()).Returns(Result<ConversationSummary>.Invalid("There is no agent named Nobody."));

        var result = await _controller.CreateConversation(request, TestContext.Current.CancellationToken);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeleteConversation_ExistingId_ReturnsNoContent()
    {
        var id = Guid.NewGuid();
        _service.DeleteConversationAsync(id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _controller.DeleteConversation(id, TestContext.Current.CancellationToken);

        result.Should().BeOfType<NoContentResult>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RenameConversation_ReturnsNoContent_OrNotFound(bool renamed)
    {
        var id = Guid.NewGuid();
        _service.RenameConversationAsync(id, "Trip planning", Arg.Any<CancellationToken>()).Returns(renamed);

        var result = await _controller.RenameConversation(id, new UpdateTitleRequest("Trip planning"), TestContext.Current.CancellationToken);

        if (renamed)
        {
            result.Should().BeOfType<NoContentResult>();
        }
        else
        {
            result.Should().BeOfType<NotFoundResult>();
        }
    }
}
