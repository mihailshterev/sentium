using System.Text.Json.Serialization;

namespace Sentium.Praxis.Features.Workflows.Squad;

/// <summary>
/// Risk of acting on a squad's answer.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<RiskLevel>))]
public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}
