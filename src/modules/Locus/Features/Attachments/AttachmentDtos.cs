using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Results;
using Sentium.Locus.Entities;
using Sentium.Locus.Features.Locations;

namespace Sentium.Locus.Features.Attachments;

public sealed record AttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    AttachmentKind Kind,
    bool IsImage,
    string ContentUrl,
    DateTimeOffset CreatedAt,
    string? CreatedBy);
