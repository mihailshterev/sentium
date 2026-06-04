using Sentium.Shared.Constants;

namespace Sentium.Identity.Core.Security;

/// <summary>
/// System role constants. The names alias the shared <see cref="SecurityRoles"/> so there is a
/// single source of truth across services; this type adds the Identity-domain hierarchy helpers.
/// </summary>
public static class Roles
{
    public const string Sovereign = SecurityRoles.Sovereign;
    public const string Member = SecurityRoles.Member;

    public static readonly IReadOnlyList<string> Hierarchy = [Member, Sovereign];
    public static bool IsValid(string role) => Hierarchy.Contains(role);
}
