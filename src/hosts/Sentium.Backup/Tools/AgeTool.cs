namespace Sentium.Backup.Tools;

/// <summary>
/// File encryption with <see href="https://age-encryption.org">age</see>. Backups encrypt to a public key only,
/// so the host can write archives it cannot read back; the identity file is supplied at restore time.
/// </summary>
internal static class AgeTool
{
    public static Task EncryptAsync(string inputPath, string outputPath, string recipient, CancellationToken ct)
        => ProcessRunner.RunAsync("age", ["--encrypt", "--recipient", recipient, "--output", outputPath, inputPath], ct);

    public static Task DecryptAsync(string inputPath, string outputPath, string identityPath, CancellationToken ct)
        => ProcessRunner.RunAsync("age", ["--decrypt", "--identity", identityPath, "--output", outputPath, inputPath], ct);
}
