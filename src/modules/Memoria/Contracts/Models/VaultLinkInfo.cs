namespace Sentium.Memoria.Contracts.Models;

/// <param name="Target">The note the link points to, <see langword="null"/> while no synced note matches it.</param>
public sealed record VaultLinkInfo(string Text, string? Heading, bool IsEmbed, VaultNoteRef? Target);
