namespace Sentium.Praxis.Common.Streaming;

internal readonly record struct StreamFrame(long Seq, AgentStreamUpdate Update);
