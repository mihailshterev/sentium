namespace Sentium.Memoria.Features.Sync;

/// <summary>
/// Every note currently in the vault. Notes Sentium holds that are not listed are removed.
/// </summary>
internal sealed record VaultManifestRequest(IReadOnlyList<ManifestEntry> Notes);
