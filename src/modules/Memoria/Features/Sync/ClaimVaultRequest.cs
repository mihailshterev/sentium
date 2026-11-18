namespace Sentium.Memoria.Features.Sync;

/// <param name="Name">The vault's name in Obsidian (its folder name).</param>
/// <param name="Takeover">Move the vault to this Device even though another paired Device holds it.</param>
internal sealed record ClaimVaultRequest(string Name, bool Takeover = false);
