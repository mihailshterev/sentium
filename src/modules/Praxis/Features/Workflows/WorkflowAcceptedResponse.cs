namespace Sentium.Praxis.Features.Workflows;

public sealed record WorkflowAcceptedResponse(string EventId, string? CorrelationId = null, string Status = "Accepted");
