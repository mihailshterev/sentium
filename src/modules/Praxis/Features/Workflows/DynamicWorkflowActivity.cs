using System.Text.Json.Serialization;

namespace Sentium.Praxis.Features.Workflows;

public sealed record DynamicWorkflowActivity(
    [property: JsonPropertyName("activity")] string Activity,
    [property: JsonPropertyName("user")] string User);
