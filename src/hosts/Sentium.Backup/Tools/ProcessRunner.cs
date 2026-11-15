using System.Diagnostics;
using System.Text;

namespace Sentium.Backup.Tools;

/// <summary>
/// Runs one of the command-line tools baked into the image and fails the backup or restore with the tool's own
/// error output when it exits non-zero.
/// </summary>
internal static class ProcessRunner
{
    private const int MaxErrorLength = 2_000;

    public static async Task<string> RunAsync(string fileName, IEnumerable<string> arguments, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new BackupFailedException($"Could not start {fileName}.");

        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        var output = await stdout;
        var error = await stderr;

        if (process.ExitCode != 0)
        {
            throw new BackupFailedException($"{fileName} exited with code {process.ExitCode}: {Truncate(error)}");
        }

        return output;
    }

    private static string Truncate(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= MaxErrorLength ? trimmed : new StringBuilder(trimmed, 0, MaxErrorLength, MaxErrorLength + 1).Append('…').ToString();
    }
}
