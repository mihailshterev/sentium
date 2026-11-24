namespace Sentium.Praxis.Features.Files;

/// <summary>
/// Pending, then Processing, then Completed or Failed.
/// </summary>
internal enum FileProcessingStatus
{
    Pending,
    Processing,
    Completed,

    /// <summary>
    /// The reason is in <see cref="Entities.ProjectFile.ProcessingError"/>.
    /// </summary>
    Failed
}
