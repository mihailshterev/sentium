namespace Sentium.Scheduling.Features.BackgroundJobs;

/// <param name="State">NotStarted, Running, Stopped, Faulted, or Hosted for a service with no long-running loop.</param>
/// <param name="Error">Why a faulted service stopped.</param>
internal sealed record BackgroundServiceResponse(string Name, string Type, string Module, string State, string? Error);
