namespace Sentium.Locus.Entities;

/// <summary>
/// A file (photo, manual, receipt, ...) stored in blob storage for an asset.
/// </summary>
public sealed class AssetAttachment
{
    public Guid Id { get; set; }

    public Guid AssetId { get; set; }

    /// <summary>
    /// Blob name, always <c>{assetId}/{id}</c>; never derived from user input.
    /// </summary>
    public string BlobName { get; set; } = null!;

    /// <summary>
    /// Sanitized original file name. Kept only here (not in blob metadata, which must be ASCII).
    /// </summary>
    public string FileName { get; set; } = null!;

    /// <summary>
    /// Content type detected by the server from the file's leading bytes.
    /// </summary>
    public string ContentType { get; set; } = null!;

    public long SizeBytes { get; set; }

    public AttachmentKind Kind { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public Asset Asset { get; set; } = null!;
}
