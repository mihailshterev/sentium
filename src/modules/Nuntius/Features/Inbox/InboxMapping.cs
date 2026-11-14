using System.Linq.Expressions;
using Sentium.Nuntius.Entities;

namespace Sentium.Nuntius.Features.Inbox;

internal static class InboxMapping
{
    public static readonly Expression<Func<Notification, NotificationDto>> Projection = n => new NotificationDto(
        n.Id, n.Category, n.Severity, n.Title, n.Body, n.Link, n.Occurrences, n.CreatedAt, n.UpdatedAt, n.ReadAt);

    private static readonly Func<Notification, NotificationDto> Compiled = Projection.Compile();

    public static NotificationDto ToDto(Notification notification) => Compiled(notification);
}
