using Sentium.Infrastructure.BackgroundWork;

namespace Sentium.Scheduling.Features.BackgroundJobs;

/// <param name="Services">The long-running workers hosted in Sentium.Web.</param>
/// <param name="Running">Work in progress right now.</param>
internal sealed record BackgroundJobsResponse(IReadOnlyList<BackgroundServiceResponse> Services, IReadOnlyList<BackgroundActivity> Running);
