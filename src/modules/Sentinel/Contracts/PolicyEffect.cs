using System.Text.Json.Serialization;

namespace Sentium.Sentinel.Contracts;

// Serialized by name: API clients read these as strings.
[JsonConverter(typeof(JsonStringEnumConverter<PolicyEffect>))]
public enum PolicyEffect
{
    Allow,
    Deny,
    DenyWithAlert
}
