namespace AgentRuntime.Core.Agents;

public sealed record AgentTask(
    AgentRole Role,
    string Instruction
);
