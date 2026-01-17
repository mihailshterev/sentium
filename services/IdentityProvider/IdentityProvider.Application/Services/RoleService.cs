using IdentityProvider.Application.Abstractions;
using IdentityProvider.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace IdentityProvider.Application.Services;

public sealed class RoleService(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager) : IRoleService
{
    public async Task AssignRoleAsync(Guid userId, string roleName, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null) throw new Exception("User not found");

        if (!await roleManager.RoleExistsAsync(roleName))
            await roleManager.CreateAsync(new ApplicationRole());

        await userManager.AddToRoleAsync(user, roleName);
    }

    public async Task RemoveRoleAsync(Guid userId, string roleName, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null) throw new Exception("User not found");

        await userManager.RemoveFromRoleAsync(user, roleName);
    }

    public async Task<IList<string>> GetRolesAsync(Guid userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null) throw new Exception("User not found");

        var roles = await userManager.GetRolesAsync(user);
        return roles;
    }

    public async Task<bool> UserHasRoleAsync(Guid userId, string roleName, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null) throw new Exception("User not found");

        return await userManager.IsInRoleAsync(user, roleName);
    }
}
