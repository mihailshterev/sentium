namespace Sentium.AgentRuntime.Core.Workflows;

public record AgentStreamUpdate(string Author, string Text, string Type = "message");