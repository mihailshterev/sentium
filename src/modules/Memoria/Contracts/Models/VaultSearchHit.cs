namespace Sentium.Memoria.Contracts.Models;

/// <param name="Snippet">The best matching chunk of the note, headed by its title and heading path.</param>
public sealed record VaultSearchHit(VaultNoteRef Note, float Score, string Snippet);
