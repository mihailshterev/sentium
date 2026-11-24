using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Sentium.Core.Results;
using Sentium.Praxis.Features.Conversations;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Conversations;

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
        _service.GetConversationsAsync(1, 20, null, false, Arg.Any<CancellationToken>()).Returns(paged);

        var result = await _controller.GetConversations(1, 20, null, false, TestContext.Current.CancellationToken);

        Assert.Equal(paged, Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task GetConversation_ExistingId_ReturnsOk()
    {
        var id = Guid.NewGuid();
        var expected = new ConversationResponse(id, "Chat", "gemma3:1b", DateTime.UtcNow, []);
        _service.GetConversationAsync(id, 100, Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _controller.GetConversation(id, 100, TestContext.Current.CancellationToken);

        Assert.Equal(expected, Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task CreateConversation_ValidRequest_ReturnsCreated()
    {
        var request = new CreateConversationRequest("New Chat", "gemma3:1b");
        var created = new ConversationSummary(Guid.NewGuid(), "New Chat", "gemma3:1b", DateTime.UtcNow);
        _service.CreateConversationAsync(request, Arg.Any<CancellationToken>()).Returns(Result<ConversationSummary>.Success(created));

        var result = await _controller.CreateConversation(request, TestContext.Current.CancellationToken);

        Assert.Equal(created, Assert.IsType<CreatedAtActionResult>(result).Value);
    }

    [Fact]
    public async Task CreateConversation_UnknownAgent_ReturnsBadRequest()
    {
        var request = new CreateConversationRequest(null, "gemma3:1b", "Nobody");
        _service.CreateConversationAsync(request, Arg.Any<CancellationToken>()).Returns(Result<ConversationSummary>.Invalid("There is no agent named Nobody."));

        var result = await _controller.CreateConversation(request, TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DeleteConversation_ExistingId_ReturnsNoContent()
    {
        var id = Guid.NewGuid();
        _service.DeleteConversationAsync(id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _controller.DeleteConversation(id, TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
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
            Assert.IsType<NoContentResult>(result);
        }
        else
        {
            Assert.IsType<NotFoundResult>(result);
        }
    }
}
