using System.Linq.Expressions;
using Sentium.Praxis.Dtos;
using Sentium.Praxis.Entities;

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
            a.UpdatedAt);
}
