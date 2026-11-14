using Sentium.Core.Common;

namespace Sentium.Praxis.Entities;

internal sealed class Workflow : IUserOwned
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<WorkflowAgent> WorkflowAgents { get; set; } = [];
}
