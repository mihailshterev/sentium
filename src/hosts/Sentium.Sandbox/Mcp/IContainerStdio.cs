namespace Sentium.Sandbox.Mcp;

/// <summary>
/// The container side of an attached stdio session: what it writes to stdout/stderr and a sink for its stdin.
/// </summary>
internal interface IContainerStdio : IDisposable
{
    Task<StdioChunk> ReadAsync(byte[] buffer, CancellationToken ct);

    Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct);

    /// <summary>
    /// Signals end-of-input to the process.
    /// </summary>
    void CloseInput();
}
