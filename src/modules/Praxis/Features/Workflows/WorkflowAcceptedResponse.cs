namespace Sentium.Praxis.Features.Workflows;

internal sealed record WorkflowAcceptedResponse(string EventId, string? CorrelationId = null, string Status = "Accepted");
