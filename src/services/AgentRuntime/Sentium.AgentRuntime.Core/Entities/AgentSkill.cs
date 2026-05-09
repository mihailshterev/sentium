namespace Sentium.AgentRuntime.Core.Entities;

public sealed class AgentSkill
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Instructions { get; set; } = null!;
    public AgentSkillType SkillType { get; set; }
    public string? FileName { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum AgentSkillType
{
    Custom = 0,
    Uploaded = 1,
}
