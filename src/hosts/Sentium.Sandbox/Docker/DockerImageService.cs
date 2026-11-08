using System.Collections.Concurrent;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace Sentium.Sandbox.Docker;

/// <summary>
/// Makes sure an image is present locally, pulling it once (serialized per image) when it is not.
/// </summary>
internal sealed class DockerImageService(IDockerClient dockerClient, ILogger<DockerImageService> logger)
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> imageLocks = new(StringComparer.Ordinal);

    public async Task EnsureImageAsync(string image, CancellationToken ct)
    {
        var semaphore = imageLocks.GetOrAdd(image, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);
        try
        {
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
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Could not check local image cache for {Image}. Proceeding with pull.", image);
            }

            var (name, tag) = SplitImageRef(image);

            logger.LogInformation("Pulling Docker image {Image}...", image);

            await dockerClient.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = name, Tag = tag },
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

    /// <summary>
    /// Splits an image reference for the pull API. A digest reference (<c>repo@sha256:…</c>) is pulled whole; otherwise
    /// the tag is whatever follows the last ':' after the last '/' (so a registry port such as
    /// <c>localhost:5000/img</c> is not mistaken for a tag), defaulting to <c>latest</c>.
    /// </summary>
    internal static (string Name, string? Tag) SplitImageRef(string imageRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageRef);

        if (imageRef.Contains('@', StringComparison.Ordinal))
        {
            return (imageRef, null);
        }

        var lastSlash = imageRef.LastIndexOf('/');
        var lastColon = imageRef.LastIndexOf(':');

        return lastColon > lastSlash ? (imageRef[..lastColon], imageRef[(lastColon + 1)..]) : (imageRef, "latest");
    }
}
