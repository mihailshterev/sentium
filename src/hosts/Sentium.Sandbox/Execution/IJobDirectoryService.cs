using Sentium.Sandbox.Models;

namespace Sentium.Sandbox.Execution;

/// <summary>
/// The ephemeral on-host job directory that is bind-mounted into the sandbox container.
/// </summary>
internal interface IJobDirectoryService
{
    Task<JobContext> CreateJobAsync(ExecutionRequest request);

    Task CleanupJobAsync(JobContext context);
}
