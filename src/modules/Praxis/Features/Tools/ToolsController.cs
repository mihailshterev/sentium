using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Praxis.Contracts;

namespace Sentium.Praxis.Features.Tools;

/// <summary>
/// The tools agents can be given.
/// </summary>
[ApiController]
[Authorize]
[Route($"{PraxisRoutes.Prefix}/tools")]
internal sealed class ToolsController(IToolCatalog catalog) : ControllerBase
{
    /// <summary>
    /// Every registered tool with its policy (resource, action, risk, approval), ordered by resource then name.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ToolDescriptor>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ToolDescriptor>> GetTools() => Ok(catalog.GetTools());
}
