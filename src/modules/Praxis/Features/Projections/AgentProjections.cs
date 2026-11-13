using System.Linq.Expressions;
using Sentium.Praxis.Entities;
using Sentium.Praxis.Features.Agents;

namespace Sentium.Praxis.Features.Projections;

internal static class AgentProjections
{
    internal static Expression<Func<Agent, AgentResponse>> ToResponse()
        => a => new AgentResponse(
            a.Id,
            a.Name,
            a.Description,
            a.Model,
            a.CreatedAt,
            a.UpdatedAt,
            a.Tools);
}
