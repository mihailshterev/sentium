namespace Sentium.Sandbox.Mcp;

internal readonly record struct StdioChunk(int Count, StdioTarget Target, bool EndOfStream);
