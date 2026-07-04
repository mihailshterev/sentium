namespace Sentium.AgentRuntime.Core.Tools;

public enum AgentToolParameterType
{
    String,
    Integer,
    Number,
    Boolean
}

public sealed record AgentToolParameter(
    string Name,
    string Description,
    AgentToolParameterType Type = AgentToolParameterType.String,
    bool Required = true,
    IReadOnlyList<string>? EnumValues = null
);
