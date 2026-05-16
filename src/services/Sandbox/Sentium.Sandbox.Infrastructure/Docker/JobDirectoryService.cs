using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sentium.Sandbox.Application.Options;
using Sentium.Sandbox.Core.Interfaces;
using Sentium.Sandbox.Core.Models;

namespace Sentium.Sandbox.Infrastructure.Docker;

/// <summary>
/// Creates and manages the temporary per-job working directory on the host.
/// </summary>
internal sealed class JobDirectoryService(IOptions<SandboxOptions> options, ILogger<JobDirectoryService> logger) : IJobDirectoryService
{
    private readonly string _baseDirectory = string.IsNullOrWhiteSpace(options.Value.JobBaseDirectory)
        ? Path.Combine(Path.GetTempPath(), "sentium-sandbox")
        : options.Value.JobBaseDirectory;

    /// <inheritdoc />
    public Task<JobContext> CreateJobAsync(ExecutionRequest request)
    {
        var jobId = Guid.NewGuid();
        var jobDirectory = Path.Combine(_baseDirectory, jobId.ToString("N"));

        Directory.CreateDirectory(jobDirectory);

        logger.LogDebug("Created job directory {JobDirectory} for job {JobId}", jobDirectory, jobId);

        return Task.FromResult(new JobContext
        {
            JobId = jobId,
            JobDirectory = jobDirectory,
            Language = request.Language
        });
    }

    /// <inheritdoc />
    public Task CleanupJobAsync(JobContext context)
    {
        try
        {
            if (Directory.Exists(context.JobDirectory))
            {
                Directory.Delete(context.JobDirectory, recursive: true);
                logger.LogDebug("Cleaned up job directory {JobDirectory}", context.JobDirectory);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete job directory {JobDirectory} for job {JobId}. Orphaned directory may require manual cleanup.", context.JobDirectory, context.JobId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates that <paramref name="relativeFileName"/> resolves to a path that
    /// stays strictly within <paramref name="jobDirectory"/>.
    /// Throws <see cref="ArgumentException"/> on path-traversal attempts.
    /// </summary>
    internal static string ResolveAndValidatePath(string jobDirectory, string relativeFileName)
    {
        if (string.IsNullOrWhiteSpace(relativeFileName))
        {
            throw new ArgumentException("FileContext entry has an empty FileName.");
        }

        var fullPath = Path.GetFullPath(Path.Combine(jobDirectory, relativeFileName));
        var normalizedBase = Path.GetFullPath(jobDirectory);

        var prefix = normalizedBase.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Security violation: '{relativeFileName}' resolves outside the job directory.");
        }

        return fullPath;
    }
}
