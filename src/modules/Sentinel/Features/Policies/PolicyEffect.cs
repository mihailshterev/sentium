using System.Text.Json.Serialization;

namespace Sentium.Sentinel.Features.Policies;

// Serialized by name: the Sentinel API has always exposed these as strings.
[JsonConverter(typeof(JsonStringEnumConverter<PolicyEffect>))]
internal enum PolicyEffect
{
    Allow,
    Deny,
    DenyWithAlert
}
