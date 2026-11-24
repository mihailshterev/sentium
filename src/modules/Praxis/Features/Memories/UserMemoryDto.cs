namespace Sentium.Praxis.Features.Memories;

internal sealed record UserMemoryDto(Guid Id, string Content, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
