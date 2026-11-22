namespace Sentium.Infrastructure.Realtime;

/// <summary>
/// Who receives a <see cref="RealtimeEvent"/>. A Sovereign gets another user's events only when the event says so,
/// so private modules (Tempus, Nuntius) keep their no-bypass rule.
/// </summary>
public readonly record struct RealtimeAudience
{
    private RealtimeAudience(Guid? userId, bool includesSovereigns, bool isEveryone)
    {
        UserId = userId;
        IncludesSovereigns = includesSovereigns;
        IsEveryone = isEveryone;
    }

    public Guid? UserId { get; }

    public bool IncludesSovereigns { get; }

    public bool IsEveryone { get; }

    /// <summary>
    /// Every signed-in user: for shared data such as the household inventory or the health of the system.
    /// </summary>
    public static RealtimeAudience Everyone { get; } = new(null, includesSovereigns: true, isEveryone: true);

    public static RealtimeAudience Sovereigns { get; } = new(null, includesSovereigns: true, isEveryone: false);

    /// <param name="userId">The user the data belongs to.</param>
    /// <param name="includeSovereigns">Whether Sovereigns, who can see this data for every user, get it too.</param>
    public static RealtimeAudience User(Guid userId, bool includeSovereigns = false) => new(userId, includeSovereigns, isEveryone: false);

    public bool Includes(Guid userId, bool isSovereign) => IsEveryone || UserId == userId || (IncludesSovereigns && isSovereign);
}
