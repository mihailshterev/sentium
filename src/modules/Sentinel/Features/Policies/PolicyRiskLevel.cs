using System.Text.Json.Serialization;

namespace Sentium.Sentinel.Features.Policies;

// Serialized by name: API clients read these as strings.
[JsonConverter(typeof(JsonStringEnumConverter<PolicyRiskLevel>))]
internal enum PolicyRiskLevel
{
    Low,
    Medium,
    High,
    Critical
}
