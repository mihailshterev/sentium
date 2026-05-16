namespace Sentium.Sandbox.Application.Options;

public sealed class SandboxOptions
{
    public const string SectionName = "Sandbox";
    public string PythonImage { get; set; } = "python:3.12-slim";
    public string NodeImage { get; set; } = "node:22-alpine";
    public long MemoryLimitBytes { get; set; } = 256 * 1024 * 1024;
    public double CpuLimit { get; set; } = 0.5;
    public long PidsLimit { get; set; } = 128;
    public long NoFileLimitSoft { get; set; } = 64;
    public long NoFileLimitHard { get; set; } = 128;
    public int ExecutionTimeoutSeconds { get; set; } = 30;
    public string JobBaseDirectory { get; set; } = string.Empty;
    public int MaxCodeSizeBytes { get; set; } = 512 * 1024;
    public int MaxFileContextEntries { get; set; } = 20;
    public int MaxFileContentBytes { get; set; } = 1024 * 1024;
    public string DockerHost { get; set; } = string.Empty;
    public bool PullImageIfMissing { get; set; } = true;
    public bool ReadonlyRootFs { get; set; } = true;
    public string SeccompProfile { get; set; } = string.Empty;
    public string TmpfsSizeMb { get; set; } = "64m";
    public bool HarvestArtifacts { get; set; } = true;
    public string ArtifactContainerName { get; set; } = "sandbox-artifacts";
    public long MaxArtifactSizeBytes { get; set; } = 50 * 1024 * 1024;
}
