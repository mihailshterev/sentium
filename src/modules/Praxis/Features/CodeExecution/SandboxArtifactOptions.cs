namespace Sentium.Praxis.Features.CodeExecution;

/// <summary>
/// Where the Sandbox service stores harvested artifacts. Bound from the same <c>Sandbox</c> section the
/// Sandbox host reads, so both sides agree on the blob container.
/// </summary>
public sealed class SandboxArtifactOptions
{
    public const string SectionName = "Sandbox";

    public string ArtifactContainerName { get; set; } = "sandbox-artifacts";
}
