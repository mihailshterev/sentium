namespace Sentium.Memoria.Contracts.Models;

/// <param name="ObsidianUri">An <c>obsidian://open</c> link that opens the note in the Obsidian app.</param>
public sealed record VaultNoteRef(Guid Id, string Path, string Title, string ObsidianUri, IReadOnlyList<string> Tags);
