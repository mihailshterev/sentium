namespace Sentium.Praxis.Features.Workflows;

public record AgentStreamUpdate(string Author, string Text, string Type = "message");
