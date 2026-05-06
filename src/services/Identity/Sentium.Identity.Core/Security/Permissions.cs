namespace Sentium.Identity.Core.Security;

/// <summary>
/// Functional permission flags. Each flag maps to a discrete capability in the system.
/// The <see cref="PermissionMap"/> dictionary defines which roles carry which flags.
/// </summary>
public static class Permissions
{
    // User management
    public const string ViewUsers = "users:view";
    public const string CreateUsers = "users:create";
    public const string EditUsers = "users:edit";
    public const string DeleteUsers = "users:delete";
    public const string AssignRoles = "users:assign_roles";

    // Asset & workspace access
    public const string ViewAssets = "assets:view";
    public const string ManageAssets = "assets:manage";
    public const string ViewWorkspaces = "workspaces:view";
    public const string ManageWorkspaces = "workspaces:manage";

    // Workflow & orchestration
    public const string ViewWorkflows = "workflows:view";
    public const string ManageWorkflows = "workflows:manage";
    public const string TriggerWorkflows = "workflows:trigger";

    // Agents
    public const string ViewAgents = "agents:view";
    public const string ManageAgents = "agents:manage";

    // Security
    public const string ViewSentinel = "sentinel:view";
    public const string ManageSentinel = "sentinel:manage";
    public const string ViewWatchdog = "watchdog:view";

    // System
    public const string ViewSystem = "system:view";
    public const string ManageSystem = "system:manage";

    /// <summary>
    /// Canonical permission mapping per role. The set for each role is the complete list of
    /// permissions that role possesses — permissions are NOT inherited from lower roles.
    /// Use <see cref="GetPermissions"/> which merges inherited permissions.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> RolePermissions =
        new Dictionary<string, IReadOnlySet<string>>
        {
            [Roles.Guest] = new HashSet<string>
            {
                ViewAssets,
                ViewWorkspaces,
                ViewWorkflows,
                ViewAgents,
            },
            [Roles.Member] = new HashSet<string>
            {
                ViewAssets,    ManageAssets,
                ViewWorkspaces, ManageWorkspaces,
                ViewWorkflows,  ManageWorkflows, TriggerWorkflows,
                ViewAgents,     ManageAgents,
                ViewSentinel,
                ViewWatchdog,
            },
            [Roles.Sovereign] = new HashSet<string>
            {
                ViewUsers,    CreateUsers,   EditUsers,   DeleteUsers, AssignRoles,
                ViewAssets,   ManageAssets,
                ViewWorkspaces, ManageWorkspaces,
                ViewWorkflows, ManageWorkflows, TriggerWorkflows,
                ViewAgents,   ManageAgents,
                ViewSentinel, ManageSentinel,
                ViewWatchdog,
                ViewSystem,   ManageSystem,
            },
        };

    /// <summary>Returns the permission set for the given role, or an empty set for unknown roles.</summary>
    public static IReadOnlySet<string> GetPermissions(string role)
    {
        return RolePermissions.TryGetValue(role, out var perms) ? perms : new HashSet<string>();
    }

    /// <summary>Checks whether <paramref name="role"/> has <paramref name="permission"/>.</summary>
    public static bool HasPermission(string role, string permission)
    {
        return GetPermissions(role).Contains(permission);
    }
}
