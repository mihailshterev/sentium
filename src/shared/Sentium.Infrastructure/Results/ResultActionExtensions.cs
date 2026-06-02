using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sentium.Shared.Results;

namespace Sentium.Infrastructure.Results;

public static class ResultActionExtensions
{
    public static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller)
    {
        ArgumentNullException.ThrowIfNull(controller);

        return result.Status switch
        {
            ResultStatus.Success => controller.Ok(result.Value),
            ResultStatus.NotFound => controller.NotFound(),
            ResultStatus.Conflict => controller.Conflict(new ProblemDetails
            {
                Title = "Conflict",
                Detail = result.Error,
                Status = StatusCodes.Status409Conflict
            }),
            _ => controller.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
