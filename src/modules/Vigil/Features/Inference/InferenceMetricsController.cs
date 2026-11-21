using Microsoft.AspNetCore.Mvc;
using Sentium.Infrastructure.Security;

namespace Sentium.Vigil.Features.Inference;

[ApiController]
[AuthorizeSovereign]
[Route($"{VigilRoutes.Prefix}/inference")]
internal sealed class InferenceMetricsController(IInferenceMetricsQuery query) : ControllerBase
{
    /// <summary>
    /// Live model states plus throughput, queue, usage and model loads over <paramref name="range"/>
    /// (<c>1h</c>, <c>6h</c>, <c>24h</c> (default), <c>7d</c> or <c>30d</c>).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<InferenceOverviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get([FromQuery] string? range, CancellationToken ct)
    {
        if (InferenceRange.Find(range) is not { } resolved)
        {
            var names = string.Join(", ", InferenceRange.All.Select(r => r.Name));
            return Problem(detail: $"Unknown range '{range}'. Use one of: {names}.", statusCode: StatusCodes.Status400BadRequest);
        }

        return Ok(await query.GetOverviewAsync(resolved, ct));
    }
}
