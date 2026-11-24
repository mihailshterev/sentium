namespace Sentium.Praxis.Features.Memories;

/// <summary>
/// The user's personal memories: rows in Praxis plus one vector each in Codex. Every call names the user explicitly
/// and sees only that user's memories, whatever the caller's role, because agent tools run outside the user's request.
/// </summary>
internal interface IUserMemoryService
{
    /// <summary>
    /// The user's memories, most recently changed first.
    /// </summary>
    Task<IReadOnlyList<UserMemoryDto>> ListAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Saves a memory, or replaces an existing one that says nearly the same thing so restating a fact never
    /// duplicates it.
    /// </summary>
    Task<RememberResult> RememberAsync(Guid userId, string content, CancellationToken ct = default);

    /// <returns><see langword="null"/> when the user has no memory with <paramref name="memoryId"/>.</returns>
    Task<UserMemoryDto?> UpdateAsync(Guid userId, Guid memoryId, string content, CancellationToken ct = default);

    /// <returns><see langword="false"/> when the user has no memory with <paramref name="memoryId"/>.</returns>
    Task<bool> ForgetAsync(Guid userId, Guid memoryId, CancellationToken ct = default);

    /// <summary>
    /// The user's memories most similar to <paramref name="query"/>, best first.
    /// </summary>
    Task<IReadOnlyList<RecalledMemory>> RecallAsync(Guid userId, string query, int limit, CancellationToken ct = default);
}
