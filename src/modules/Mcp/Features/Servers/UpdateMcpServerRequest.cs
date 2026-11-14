namespace Sentium.Mcp.Features.Servers;

/// <param name="Variables">Like <paramref name="Secrets"/> but stored in plain text and shown in the portal; a name is either a secret or a variable.</param>
/// <param name="Secrets">Changes to the stored secrets: <see langword="null"/> keeps them all; an entry with a value sets it, an entry with a <see langword="null"/> value removes it, unmentioned names are kept.</param>
public sealed record UpdateMcpServerRequest(
    string DisplayName,
    string? Description,
    string? Endpoint,
    string? Image,
    IReadOnlyList<string>? Args,
    string? Network,
    int? MemoryMb,
    double? CpuLimit,
    IReadOnlyDictionary<string, string?>? Secrets,
    bool Enabled,
    IReadOnlyDictionary<string, string?>? Variables = null);
