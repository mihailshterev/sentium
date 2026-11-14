using Sentium.Nuntius.Contracts;

namespace Sentium.Nuntius.Features.Inbox;

internal sealed record NotificationDto(
    Guid Id,
    string Category,
    NotificationSeverity Severity,
    string Title,
    string? Body,
    string? Link,
    int Occurrences,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ReadAt);
