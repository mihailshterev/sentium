using Sentium.Core.Common;

namespace Sentium.Memoria.Entities;

/// <summary>
/// A user's Obsidian vault as last synced by Sentium Satelles. One per user; only the Device that claimed it may
/// sync it, so two PCs never fight over the same notes.
/// </summary>
internal sealed class Vault : IUserOwned, INonAudited
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// The vault's name in Obsidian (its folder name), needed to build <c>obsidian://open</c> links.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    public Guid? DeviceId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastSyncAt { get; set; }
}
