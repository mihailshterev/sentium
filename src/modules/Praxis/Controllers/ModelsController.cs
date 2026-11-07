using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Sentium.Core.Constants;
using Sentium.Infrastructure.Inference;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Dtos;
using Sentium.Praxis.Features.Models;
using Sentium.Praxis.Features.Registry;

namespace Sentium.Praxis.Controllers;

/// <summary>
/// Lists the chat models the inference server serves and lets a Sovereign load or unload them.
/// Which models exist is deployment configuration (<c>infra/llm/models.ini</c> and <c>models.lock</c>),
/// so there is no pull or delete.
/// </summary>
[ApiController]
[Authorize]
[Route($"{PraxisRoutes.Prefix}/models")]
public sealed class ModelsController(
    IModelCatalog catalog,
    IRegistrySettingsService registrySettings,
    IOptions<InferenceOptions> inferenceOptions) : ControllerBase
{
    private bool IsSovereign => RoleClaims.IsInRole(User, SecurityRoles.Sovereign);

    /// <summary>
    /// Lists the declared chat models with their runtime status, plus the effective default model.
    /// </summary>
    /// <param name="ct">Cancellation token to abort the request.</param>
    /// <response code="200">The models and the default model.</response>
    /// <response code="503">The inference server is unreachable or failed.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ModelsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetModels(CancellationToken ct)
    {
        var models = await catalog.ListAsync(ct);
        if (models is null)
        {
            return Problem(detail: "The inference server is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var settings = await registrySettings.GetAsync(userId: null, ct);
        var defaultModel = settings.Inference?.DefaultModel ?? inferenceOptions.Value.DefaultModel;

        return Ok(new ModelsResponse(defaultModel, models.Select(m => new ModelInfo(m.Id, m.Status, m.ContextLength)).ToList()));
    }

    /// <summary>
    /// Starts loading a declared model into memory, ahead of its first request. Loading is asynchronous:
    /// the model list reports <c>loading</c> until it is ready.
    /// </summary>
    /// <response code="202">The inference server started loading the model.</response>
    /// <response code="403">The caller is not a Sovereign.</response>
    /// <response code="404">The model is not declared on the inference server.</response>
    /// <response code="409">The model is still running or changing state.</response>
    /// <response code="503">The inference server is unreachable or failed.</response>
    [HttpPost("load")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> LoadModel([FromBody] ModelActionRequest request, CancellationToken ct) =>
        RunAsync(request, catalog.LoadAsync, ct);

    /// <summary>
    /// Starts unloading a model from memory to free GPU/CPU memory. It stays declared and loads again on
    /// demand (its next request) or via <c>load</c>.
    /// </summary>
    /// <response code="202">The inference server started unloading the model.</response>
    /// <response code="403">The caller is not a Sovereign.</response>
    /// <response code="404">The model is not declared on the inference server.</response>
    /// <response code="409">The model is changing state.</response>
    /// <response code="503">The inference server is unreachable or failed.</response>
    [HttpPost("unload")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> UnloadModel([FromBody] ModelActionRequest request, CancellationToken ct) =>
        RunAsync(request, catalog.UnloadAsync, ct);

    private async Task<IActionResult> RunAsync(
        ModelActionRequest request,
        Func<string, CancellationToken, Task<ModelActionResult>> action,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsSovereign)
        {
            return Forbid();
        }

        return await action(request.Model, ct) switch
        {
            ModelActionResult.Accepted => Accepted(),
            ModelActionResult.NotFound => Problem(detail: $"Model '{request.Model}' is not served by the inference server.", statusCode: StatusCodes.Status404NotFound),
            ModelActionResult.Conflict => Problem(detail: $"Model '{request.Model}' is still changing state; try again shortly.", statusCode: StatusCodes.Status409Conflict),
            _ => Problem(detail: "The inference server is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable)
        };
    }
}
