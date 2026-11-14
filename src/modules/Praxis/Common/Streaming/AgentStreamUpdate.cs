namespace Sentium.Praxis.Common.Streaming;

public record AgentStreamUpdate(string Author, string Text, string Type = "message");
