using System.Linq.Expressions;
using AgentRuntime.Core.Dtos;
using AgentRuntime.Core.Entities;

namespace AgentRuntime.Infrastructure.Projections;

internal static class AgentProjections
{
    internal static Expression<Func<Agent, AgentResponse>> ToResponse()
        => a => new AgentResponse(
            a.Id,
            a.Name,
            a.Description,
            a.Model,
            a.CreatedAt,
            a.UpdatedAt);
}
