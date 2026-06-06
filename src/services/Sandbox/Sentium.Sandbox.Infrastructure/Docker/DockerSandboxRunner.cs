using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sentium.Sandbox.Application.Artifacts;
using Sentium.Sandbox.Application.Options;
using Sentium.Sandbox.Core.Interfaces;
using Sentium.Sandbox.Core.Models;

namespace Sentium.Sandbox.Infrastructure.Docker;

/// <summary>
/// Executes code inside a Docker container with comprehensive security hardening.
/// </summary>
/// <remarks>
/// Execution lifecycle per job:
/// <list type="number">
///   <item>Create the job directory and materialize all source files.</item>
///   <item>Optionally pull the worker image.</item>
///   <item>Create a security-hardened container (NetworkDisabled, ReadonlyRootfs, CapDrop ALL, etc.).</item>
///   <item>Attach to container stdout/stderr before starting.</item>
///   <item>Start the container and wait for it to exit within the configured window.</item>
///   <item>Kill the container if the timeout fires.</item>
///   <item>Always stop, remove the container and clean up the job directory in a <c>finally</c> block.</item>
/// </list>
/// </remarks>
internal sealed class DockerSandboxRunner(
    IDockerClient dockerClient,
    IJobDirectoryService jobDirectoryService,
    ContainerConfigBuilder containerConfigBuilder,
    IArtifactService artifactService,
    IOptions<SandboxOptions> options,
    ILogger<DockerSandboxRunner> logger) : ISandboxRunner
{
    private readonly SandboxOptions _options = options.Value;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _imageLocks = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public async Task<ExecutionResult> RunAsync(ExecutionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sw = Stopwatch.StartNew();
        JobContext? jobContext = null;
        string? containerId = null;
        var timedOut = false;
        long exitCode = -1;
        var stdout = string.Empty;
        var stderr = string.Empty;
        IReadOnlyList<ArtifactRecord> artifacts = [];

        try
        {
            jobContext = await jobDirectoryService.CreateJobAsync(request);

            var codeFileName = request.Language == ExecutionLanguage.Python ? "main.py" : "main.js";
            var codeFilePath = Path.Combine(jobContext.JobDirectory, codeFileName);

            var inputFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { codeFileName };

            await File.WriteAllTextAsync(codeFilePath, request.Code, Encoding.UTF8, ct);

            foreach (var contextFile in request.FileContext)
            {
                var destPath = JobDirectoryService.ResolveAndValidatePath(jobContext.JobDirectory, contextFile.FileName);

                var destDir = Path.GetDirectoryName(destPath)!;
                if (!Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                await File.WriteAllTextAsync(destPath, contextFile.Content, Encoding.UTF8, ct);

                inputFiles.Add(contextFile.FileName.Replace('\\', '/').TrimStart('/'));
            }

            var image = request.Language == ExecutionLanguage.Python ? _options.PythonImage : _options.NodeImage;

            if (_options.PullImageIfMissing)
            {
                await EnsureImageAsync(image, ct);
            }

            var cmd = request.Language == ExecutionLanguage.Python ? new[] { "python", "-u", $"/job/{codeFileName}" } : new[] { "node", $"/job/{codeFileName}" };

            var containerParams = containerConfigBuilder.Build(image, cmd, jobContext.JobDirectory, jobContext.JobId, request.Language);

            var createResponse = await dockerClient.Containers.CreateContainerAsync(containerParams, ct);
            containerId = createResponse.ID;

            logger.LogInformation("Created sandbox container {ContainerShortId} for job {JobId} (Language={Language})", ShortId(containerId), jobContext.JobId, request.Language);

            var attachStream = await dockerClient.Containers.AttachContainerAsync(
                containerId,
                tty: false,
                new ContainerAttachParameters { Stdout = true, Stderr = true, Stream = true },
                ct
            );

            var started = await dockerClient.Containers.StartContainerAsync(containerId, null, ct);
            if (!started)
            {
                throw new InvalidOperationException($"Docker daemon refused to start container {containerId}.");
            }

            logger.LogInformation("Started sandbox container {ContainerShortId} (timeout={TimeoutSeconds}s)", ShortId(containerId), _options.ExecutionTimeoutSeconds);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.ExecutionTimeoutSeconds));

            try
            {
                using var stdoutBuffer = new MemoryStream();
                using var stderrBuffer = new MemoryStream();

                await attachStream.CopyOutputToAsync(Stream.Null, stdoutBuffer, stderrBuffer, timeoutCts.Token);

                stdout = TruncateOutput(stdoutBuffer, _options.MaxOutputBytes, "stdout");
                stderr = TruncateOutput(stderrBuffer, _options.MaxOutputBytes, "stderr");

                var inspect = await dockerClient.Containers.InspectContainerAsync(containerId, ct);
                exitCode = inspect.State.ExitCode;
                timedOut = false;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                timedOut = true;
                stderr = $"Execution timed out after {_options.ExecutionTimeoutSeconds} second(s). Container killed.";

                logger.LogWarning("Sandbox job {JobId} exceeded the {TimeoutSeconds}s execution window. Killing container {ContainerShortId}.", jobContext.JobId, _options.ExecutionTimeoutSeconds, ShortId(containerId));

                await KillContainerAsync(containerId);
            }

            if (_options.HarvestArtifacts)
            {
                artifacts = await artifactService.HarvestAsync(jobContext.JobDirectory, inputFiles, jobContext.JobId, ct);
            }

            sw.Stop();

            logger.LogInformation(
                "Sandbox job {JobId} completed. ExitCode={ExitCode} TimedOut={TimedOut} Artifacts={ArtifactCount} Duration={DurationMs}ms",
                jobContext.JobId, exitCode, timedOut, artifacts.Count, sw.ElapsedMilliseconds);

            return new ExecutionResult
            {
                Succeeded = exitCode == 0 && !timedOut,
                ExitCode = exitCode,
                Output = stdout,
                Error = stderr,
                TimedOut = timedOut,
                PolicyDenied = false,
                ContainerId = containerId,
                JobId = jobContext.JobId,
                DurationMs = sw.ElapsedMilliseconds,
                Artifacts = artifacts
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogWarning("Sandbox job {JobId} was cancelled by the caller.", jobContext?.JobId);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error executing sandbox job {JobId}.", jobContext?.JobId);

            return new ExecutionResult
            {
                Succeeded = false,
                ExitCode = -1,
                Output = stdout,
                Error = ex.Message,
                TimedOut = false,
                PolicyDenied = false,
                ContainerId = containerId,
                JobId = jobContext?.JobId ?? Guid.NewGuid(),
                DurationMs = sw.ElapsedMilliseconds,
                Artifacts = []
            };
        }
        finally
        {
            if (containerId is not null)
            {
                await RemoveContainerAsync(containerId);
            }

            if (jobContext is not null)
            {
                await jobDirectoryService.CleanupJobAsync(jobContext);
            }
        }
    }

    private async Task EnsureImageAsync(string image, CancellationToken ct)
    {
        var semaphore = _imageLocks.GetOrAdd(image, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);
        try
        {
            var (imageName, tag) = SplitImageRef(image);

            try
            {
                var existing = await dockerClient.Images.ListImagesAsync(
                    new ImagesListParameters
                    {
                        Filters = new Dictionary<string, IDictionary<string, bool>>
                        {
                            ["reference"] = new Dictionary<string, bool> { [image] = true }
                        }
                    }, ct);

                if (existing.Count > 0)
                {
                    logger.LogDebug("Docker image {Image} already present locally.", image);
                    return;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not check local image cache for {Image}. Proceeding with pull.", image);
            }

            logger.LogInformation("Pulling Docker image {Image}...", image);

            await dockerClient.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = imageName, Tag = tag },
                authConfig: null,
                new Progress<JSONMessage>(msg =>
                {
                    if (!string.IsNullOrWhiteSpace(msg.Status))
                    {
                        logger.LogDebug("Docker pull [{Image}]: {Status} {Progress}", image, msg.Status, msg.ProgressMessage);
                    }
                }),
                ct);

            logger.LogInformation("Successfully pulled Docker image {Image}.", image);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task KillContainerAsync(string containerId)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await dockerClient.Containers.KillContainerAsync(
                containerId,
                new ContainerKillParameters { Signal = "SIGKILL" },
                cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            logger.LogWarning("Timed out killing container {ContainerShortId}. Attempting hard stop.", ShortId(containerId));
            await HardStopContainerAsync(containerId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to kill container {ContainerShortId}. Attempting hard stop.", ShortId(containerId));
            await HardStopContainerAsync(containerId);
        }
    }

    private async Task HardStopContainerAsync(string containerId)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await dockerClient.Containers.StopContainerAsync(
                containerId,
                new ContainerStopParameters { WaitBeforeKillSeconds = 0 },
                cts.Token);
        }
        catch (Exception stopEx)
        {
            logger.LogError(stopEx, "Could not stop container {ContainerShortId}. Manual cleanup required.", ShortId(containerId));
        }
    }

    private async Task RemoveContainerAsync(string containerId)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await dockerClient.Containers.RemoveContainerAsync(
                containerId,
                new ContainerRemoveParameters { Force = true, RemoveVolumes = true },
                cts.Token);

            logger.LogDebug("Removed sandbox container {ContainerShortId}.", ShortId(containerId));
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            logger.LogWarning("Timed out removing container {ContainerShortId}. The container may persist on the host.", ShortId(containerId));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to remove container {ContainerShortId}. The container may persist on the host.", ShortId(containerId));
        }
    }

    private static string TruncateOutput(MemoryStream buffer, int maxBytes, string streamName)
    {
        var bytes = buffer.ToArray();
        if (bytes.Length <= maxBytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        var truncated = Encoding.UTF8.GetString(bytes, 0, maxBytes);
        return truncated + $"\n[{streamName} truncated at {maxBytes} bytes]";
    }

    private static (string imageName, string tag) SplitImageRef(string imageRef)
    {
        var lastColon = imageRef.LastIndexOf(':');
        return lastColon < 0 ? (imageRef, "latest") : (imageRef[..lastColon], imageRef[(lastColon + 1)..]);
    }

    private static string ShortId(string id) => id.Length >= 12 ? id[..12] : id;
}
