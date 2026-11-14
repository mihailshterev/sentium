using Sentium.Praxis.Common.Streaming;
using Sentium.Praxis.Features.Workflows;

namespace Sentium.Praxis.Features.Orchestration;

/// <summary>
/// The timeline of one workflow run: every entry goes live to the run's relay stream and into the log persisted with
/// the run's result.
/// </summary>
internal sealed class WorkflowTimeline(IAgentStreamPublisher stream, string streamId)
{
    public const string SystemAuthor = "System";

    private readonly StreamLogAccumulator log = new();

    public IReadOnlyList<WorkflowLogEntry> Entries => log.Entries;

    public void Post(string author, string text, string type)
    {
        stream.Publish(streamId, new AgentStreamUpdate(author, text, type));
        log.Add(author, text, type);
    }

    /// <summary>
    /// A sink that posts an agent run's updates under <paramref name="author"/>.
    /// </summary>
    public IAgentUpdateSink For(string author) => new AuthorSink(this, author);

    private sealed class AuthorSink(WorkflowTimeline timeline, string author) : IAgentUpdateSink
    {
        public Task WriteUpdateAsync(string type, string content, CancellationToken ct)
        {
            timeline.Post(author, content, type);
            return Task.CompletedTask;
        }
    }
}
