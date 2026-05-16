using Sentium.Sandbox.Core.Models;

namespace Sentium.Sandbox.Core.Interfaces;

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
    /// Implementations must be resilient — failure to clean up must be logged but must not throw.
    /// </summary>
    Task CleanupJobAsync(JobContext context);
}
