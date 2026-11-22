using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Sentium.Praxis.Features.Agents.Approvals;

/// <summary>
/// Stores a <see cref="PendingApproval"/> as JSON with the AI content serializer settings, which round-trip the approval
/// request and the function call inside it.
/// </summary>
internal static class PendingApprovalSerializer
{
    public static string Serialize(PendingApproval approval)
    {
        ArgumentNullException.ThrowIfNull(approval);
        return JsonSerializer.Serialize(approval, AIJsonUtilities.DefaultOptions);
    }

    public static PendingApproval? Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrEmpty(json);
        return JsonSerializer.Deserialize<PendingApproval>(json, AIJsonUtilities.DefaultOptions);
    }
}
