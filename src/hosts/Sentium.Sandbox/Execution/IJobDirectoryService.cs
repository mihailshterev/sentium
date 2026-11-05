using Sentium.Sandbox.Models;

namespace Sentium.Sandbox.Execution;

/// <summary>
/// Manages the ephemeral on-host job directory that is bind-mounted into the sandbox container.
/// </summary>
public interface IJobDirectoryService
{
    /// <summary>
    /// Creates a uniquely named, isolated job directory for the given execution request.
    /// </summary>
    Task<JobContext> CreateJobAsync(ExecutionRequest request);

    /// <summary>
    /// Removes the job directory and all its contents after execution completes.
    /// </summary>
    Task CleanupJobAsync(JobContext context);
}
