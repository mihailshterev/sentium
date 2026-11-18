namespace Sentium.Memoria.Contracts.Models;

/// <param name="Strength">The similarity score for <see cref="VaultRelation.Similar"/>, the number of shared neighbours for <see cref="VaultRelation.SharesLinks"/>, else 1.</param>
public sealed record VaultRelatedNote(VaultNoteRef Note, VaultRelation Relation, double Strength);
