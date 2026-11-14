using NSubstitute;
using Sentium.Nuntius.Contracts;
using Sentium.Praxis.Features.Orchestration;
using Sentium.Praxis.Features.Workflows;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Orchestration;

public sealed class WorkflowRunNotifierTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RunId = Guid.NewGuid();

    private readonly INotifier _notifier = Substitute.For<INotifier>();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static WorkflowTrigger Trigger(Guid? userId = null)
        => new() { TriggerType = "manual", Payload = """{"activity":"Summarise the inbox"}""", UserId = userId ?? UserId, StreamId = "stream-1" };

    private WorkflowRunNotifier CreateSut() => new(_notifier);

    [Fact]
    public async Task CompletedRun_NotifiesItsUser_WithALinkToTheRun()
    {
        await CreateSut().NotifyAsync(Trigger(), new PersistedRun(RunId, "Inbox summary"), WorkflowRunOutcome.Completed, null, Ct);

        await _notifier.Received(1).NotifyAsync(
            NotificationRecipient.User(UserId),
            Arg.Is<NotificationMessage>(m =>
                m.Category == NotificationCategories.WorkflowRun
                && m.Severity == NotificationSeverity.Info
                && m.Title.Contains("Inbox summary")
                && m.Link == $"/orchestration/runs/{RunId}"),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(nameof(WorkflowRunOutcome.Failed))]
    [InlineData(nameof(WorkflowRunOutcome.TimedOut))]
    [InlineData(nameof(WorkflowRunOutcome.Interrupted))]
    public async Task UnsuccessfulRun_IsAWarning_CarryingTheReason(string outcome)
    {
        await CreateSut().NotifyAsync(Trigger(), new PersistedRun(RunId, null), Enum.Parse<WorkflowRunOutcome>(outcome), "[FAILED] boom", Ct);

        await _notifier.Received(1).NotifyAsync(
            NotificationRecipient.User(UserId),
            Arg.Is<NotificationMessage>(m => m.Severity == NotificationSeverity.Warning && m.Body == "[FAILED] boom"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnpersistedRun_LinksToOrchestration_AndFallsBackToTheRequest()
    {
        await CreateSut().NotifyAsync(Trigger(), run: null, WorkflowRunOutcome.Completed, null, Ct);

        await _notifier.Received(1).NotifyAsync(
            Arg.Any<NotificationRecipient>(),
            Arg.Is<NotificationMessage>(m => m.Link == "/orchestration" && m.Title.Contains("Summarise the inbox")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UserlessRun_NotifiesNobody()
    {
        var trigger = new WorkflowTrigger { TriggerType = "system", Payload = "{}" };

        await CreateSut().NotifyAsync(trigger, new PersistedRun(RunId, null), WorkflowRunOutcome.Completed, null, Ct);

        await _notifier.DidNotReceive().NotifyAsync(Arg.Any<NotificationRecipient>(), Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }
}
