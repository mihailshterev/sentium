using System.Diagnostics;

namespace Sentium.AppHost.Config;

/// <summary>
/// Aspire has no API for a custom Docker network, so the AppHost makes sure the <c>internal</c> one exists (what
/// <c>docker-compose.yml</c> declares) before the containers start.
/// </summary>
internal static class InternalNetwork
{
    /// <summary>
    /// Creates <paramref name="name"/> as an internal network when it does not exist. An existing network that is not
    /// internal is rejected: MCP containers on it would have a route to the internet.
    /// </summary>
    public static async Task EnsureAsync(string name, CancellationToken ct)
    {
        var runtime = Environment.GetEnvironmentVariable("ASPIRE_CONTAINER_RUNTIME") is { Length: > 0 } configured ? configured : "docker";

        var inspect = await RunAsync(runtime, ["network", "inspect", "--format", "{{.Internal}}", name], ct);
        if (inspect.ExitCode == 0)
        {
            if (!inspect.Output.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"The '{name}' container network exists but is not internal, so MCP containers on it could reach the internet. " +
                    $"Remove it ('{runtime} network rm {name}') and the AppHost recreates it as an internal network.");
            }

            return;
        }

        var create = await RunAsync(runtime, ["network", "create", "--internal", name], ct);
        if (create.ExitCode != 0)
        {
            throw new InvalidOperationException($"Could not create the internal '{name}' container network: {create.Output.Trim()}");
        }
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string fileName, string[] arguments, CancellationToken ct)
    {
        var info = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException($"Could not start '{fileName}'.");

        var output = process.StandardOutput.ReadToEndAsync(ct);
        var error = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        return (process.ExitCode, process.ExitCode == 0 ? await output : await error);
    }
}
