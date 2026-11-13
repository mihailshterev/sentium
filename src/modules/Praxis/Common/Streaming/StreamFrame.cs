namespace Sentium.Praxis.Common.Streaming;

public readonly record struct StreamFrame(long Seq, AgentStreamUpdate Update);
