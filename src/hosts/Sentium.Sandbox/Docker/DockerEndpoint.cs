using System.Runtime.InteropServices;
using Sentium.Sandbox.Options;

namespace Sentium.Sandbox.Docker;

/// <summary>
/// Where the Docker daemon listens: <c>Sandbox:DockerHost</c>, else <c>DOCKER_HOST</c>, else the platform default.
/// </summary>
internal static class DockerEndpoint
{
    public const string WindowsDefault = "npipe://./pipe/docker_engine";
    public const string UnixDefault = "unix:///var/run/docker.sock";

    public static Uri Resolve(SandboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configured = !string.IsNullOrWhiteSpace(options.DockerHost)
            ? options.DockerHost
            : Environment.GetEnvironmentVariable("DOCKER_HOST");

        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? WindowsDefault : UnixDefault;
        }

        return new Uri(configured);
    }
}
