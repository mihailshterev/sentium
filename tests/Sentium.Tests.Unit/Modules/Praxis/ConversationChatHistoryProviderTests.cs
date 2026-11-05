using System.ComponentModel;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentium.Praxis.Dtos;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Conversations;
using Sentium.Praxis.Features.Tools;
using Sentium.Praxis.Features.Tools.Attributes;
using Sentium.Tests.Unit.Modules.Praxis.Support;
using Xunit;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Sentium.Tests.Unit.Modules.Praxis;

/// <summary>
/// Server-side chat history and the Agent Framework approval round trip: a run that pauses on an approval-required tool
/// is serialized, then resumed by a freshly built agent (a new request scope) with only the approval response.
/// </summary>
public sealed class ConversationChatHistoryProviderTests
{
    private readonly AgentTestHost _host = new();
    private readonly IConversationRepository _repository = Substitute.For<IConversationRepository>();
    private readonly IConversationService _conversations = Substitute.For<IConversationService>();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _conversationId = Guid.NewGuid();
    private readonly List<MessageResponse> _transcript = [];

    public ConversationChatHistoryProviderTests()
    {
        _host.RunContext.UserId = _userId;

        _repository.IsOwnedByAsync(_conversationId, _userId, Arg.Any<CancellationToken>()).Returns(true);
        _repository.GetRecentMessagesAsync(_conversationId, _userId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => _transcript.ToList());

        // The service writes into the in-memory transcript, so later loads see earlier writes - as the database would.
        _conversations
            .When(c => c.AddMessageAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>()))
            .Do(call => _transcript.Add(new MessageResponse(Guid.NewGuid(), call.ArgAt<string>(1), call.ArgAt<string>(2), DateTime.UtcNow, call.ArgAt<string?>(3))));
    }

    [Fact]
    public async Task PausedRun_ResumesFromSerializedSession_InAFreshScope_AndRunsTheApprovedToolOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        _transcript.Add(new MessageResponse(Guid.NewGuid(), "user", "earlier question", DateTime.UtcNow));
        _transcript.Add(new MessageResponse(Guid.NewGuid(), "assistant", "earlier answer", DateTime.UtcNow));

        var tool = new ApprovalTool();
        _host.Tools.Add(tool.CreateFunction());
        _host.Model.Script(new ChatResponse(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call-1", "run_report")])));
        _host.Model.Script(new ChatResponse(new ChatMessage(ChatRole.Assistant, "report done")));

        // Request 1: the run pauses on the approval request.
        var history1 = CreateProvider();
        var agent1 = await _host.CreateFactory().CreateAsync(AgentRole.GeneralAssistant, chatHistory: history1, ct: ct);
        var session1 = await agent1.CreateSessionAsync(ct);
        history1.Attach(session1, _conversationId);

        var paused = await agent1.RunAsync(new ChatMessage(ChatRole.User, "run the report"), session1, cancellationToken: ct);

        var request = paused.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().Should().ContainSingle().Subject;
        tool.Invocations.Should().Be(0);
        _transcript.Should().ContainSingle(m => m.Role == "user" && m.Content == "run the report", "the user's turn is recorded as soon as the run pauses");

        var serialized = await agent1.SerializeSessionAsync(session1, cancellationToken: ct);

        // Request 2: a new scope rebuilds the agent and resumes with only the approval response.
        var agent2 = await _host.CreateFactory().CreateAsync(AgentRole.GeneralAssistant, chatHistory: CreateProvider(), ct: ct);
        var session2 = await agent2.DeserializeSessionAsync(serialized, cancellationToken: ct);

        var resumed = await agent2.RunAsync(new ChatMessage(ChatRole.User, [request.CreateResponse(true)]), session2, cancellationToken: ct);

        tool.Invocations.Should().Be(1);
        resumed.Text.Should().Contain("report done");

        var finalRequest = _host.Model.Requests[^1].Messages;
        finalRequest.Count(m => m.Text == "run the report").Should().Be(1, "the paused question is replayed exactly once");
        finalRequest.Should().Contain(m => m.Text == "earlier question", "prior turns come from the server transcript");
        finalRequest.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Should().ContainSingle(r => r.CallId == "call-1");

        _transcript.Select(m => (m.Role, m.Content)).Should().Equal(
            ("user", "earlier question"),
            ("assistant", "earlier answer"),
            ("user", "run the report"),
            ("assistant", "report done"));
    }

    [Fact]
    public async Task Retry_AfterAFailedTurn_DoesNotSendTheUnansweredUserTurnTwice()
    {
        var ct = TestContext.Current.CancellationToken;
        _transcript.Add(new MessageResponse(Guid.NewGuid(), "user", "summarise the logs", DateTime.UtcNow));

        var history = CreateProvider();
        var agent = await _host.CreateFactory().CreateAsync(AgentRole.GeneralAssistant, chatHistory: history, ct: ct);
        var session = await agent.CreateSessionAsync(ct);
        history.Attach(session, _conversationId);

        await agent.RunAsync(new ChatMessage(ChatRole.User, "summarise the logs"), session, cancellationToken: ct);

        _host.Model.Requests[^1].Messages.Count(m => m.Role == ChatRole.User).Should().Be(1);
    }

    [Fact]
    public async Task ForeignConversation_YieldsNoHistory_AndIsNeverWritten()
    {
        var ct = TestContext.Current.CancellationToken;
        var foreign = Guid.NewGuid();
        _repository.IsOwnedByAsync(foreign, _userId, Arg.Any<CancellationToken>()).Returns(false);
        _repository.GetRecentMessagesAsync(foreign, _userId, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);

        var history = CreateProvider();
        var agent = await _host.CreateFactory().CreateAsync(AgentRole.GeneralAssistant, chatHistory: history, ct: ct);
        var session = await agent.CreateSessionAsync(ct);
        history.Attach(session, foreign);

        await agent.RunAsync(new ChatMessage(ChatRole.User, "hello"), session, cancellationToken: ct);

        await _conversations.DidNotReceive().AddMessageAsync(foreign, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>());
    }

    private ConversationChatHistoryProvider CreateProvider() =>
        new(_repository, _conversations, _host.RunContext, NullLogger<ConversationChatHistoryProvider>.Instance);

    [AgentToolPolicy(RequiresApproval = true, Resource = ToolResource.Code, Action = ToolAction.Execute)]
    private sealed class ApprovalTool : IAgentTool
    {
        public int Invocations { get; private set; }

        public AIFunction CreateFunction() => AgentToolFunction.Create(this, Run, "run_report", "Runs the report.");

        [Description("Runs the report.")]
        private string Run()
        {
            Invocations++;
            return "rows: 42";
        }
    }
}
