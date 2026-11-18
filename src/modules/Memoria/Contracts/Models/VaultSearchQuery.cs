namespace Sentium.Memoria.Contracts.Models;

/// <param name="Tag">Only notes with this tag (or a nested tag under it).</param>
/// <param name="Folder">Only notes in this folder or below it.</param>
public sealed record VaultSearchQuery(string Query, int TopK, string? Tag = null, string? Folder = null);
