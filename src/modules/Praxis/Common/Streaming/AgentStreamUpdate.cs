namespace Sentium.Praxis.Common.Streaming;

internal record AgentStreamUpdate(string Author, string Text, string Type = "message");
