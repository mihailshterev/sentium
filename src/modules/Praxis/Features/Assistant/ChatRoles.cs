using Microsoft.Extensions.AI;

namespace Sentium.Praxis.Features.Assistant;

internal static class ChatRoles
{
    public static bool IsUser(string role) => role.Equals(ChatRole.User.ToString(), StringComparison.OrdinalIgnoreCase);
}
