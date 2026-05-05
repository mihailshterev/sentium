using System.Linq.Expressions;
using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.Entities;

namespace Sentium.AgentRuntime.Infrastructure.Projections;

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
