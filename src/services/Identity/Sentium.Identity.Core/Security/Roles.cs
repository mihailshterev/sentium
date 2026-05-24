namespace Sentium.Identity.Core.Security;

/// <summary>
/// System role constants. These names are the single source of truth for role-based access.
/// </summary>
public static class Roles
{
    /// <summary>Full system access: user management, security configuration, all assets.</summary>
    public const string Sovereign = "Sovereign";

    /// <summary>Full access to assets and workflows; no administrative rights.</summary>
    public const string Member = "Member";

    /// <summary>Read-only access to specific pages or assets; intended for limited-time access.</summary>
    public const string Guest = "Guest";

    /// <summary>Ordered hierarchy — higher index = higher privilege.</summary>
    public static readonly IReadOnlyList<string> Hierarchy = [Guest, Member, Sovereign];

    /// <summary>Returns true when <paramref name="actorRole"/> outranks <paramref name="targetRole"/>.</summary>
    public static bool Outranks(string actorRole, string targetRole)
    {
        var actorRank = ((IList<string>)Hierarchy).IndexOf(actorRole);
        var targetRank = ((IList<string>)Hierarchy).IndexOf(targetRole);
        return actorRank > targetRank;
    }

    /// <summary>Returns true if the role string is one of the defined system roles.</summary>
    public static bool IsValid(string role) => Hierarchy.Contains(role);
}
