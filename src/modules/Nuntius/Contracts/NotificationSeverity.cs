using System.Text.Json.Serialization;

namespace Sentium.Nuntius.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<NotificationSeverity>))]
public enum NotificationSeverity
{
    Info,
    Warning,
    Critical
}
