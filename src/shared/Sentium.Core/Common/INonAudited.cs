namespace Sentium.Core.Common;

/// <summary>
/// Marks a high-volume entity (chunks, outbox rows, metrics) whose saves the change log leaves out.
/// </summary>
public interface INonAudited;
