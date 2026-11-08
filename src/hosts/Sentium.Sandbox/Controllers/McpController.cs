using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Sentium.Infrastructure.Security;
using Sentium.Sandbox.Contracts;
using Sentium.Sandbox.Mcp;

namespace Sentium.Sandbox.Controllers;

/// <summary>
/// Stdio MCP servers hosted for Sentium.Web. Tool payloads are the MCP SDK's protocol types, serialized with
/// <see cref="McpJsonUtilities.DefaultOptions"/>; a 404 on the tool routes means "unknown server - ensure it first".
/// </summary>
[ApiController]
[AuthorizeSystem]
[Route("mcp/servers/{name}")]
public sealed class McpController(IMcpServerHost host) : ControllerBase
{
    /// <summary>
    /// Starts the server, or keeps it running when the spec is unchanged (a changed spec replaces its container).
    /// </summary>
    [HttpPut]
    [ProducesResponseType<McpServerStatus>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> EnsureAsync(string name, [FromBody] McpServerLaunchSpec spec, CancellationToken ct)
    {
        if (Disabled() is { } disabled)
        {
            return disabled;
        }

        if (!McpServerNames.IsValid(name))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid MCP server name.");
        }

        try
        {
            return Ok(await host.EnsureAsync(name, spec, ct));
        }
        catch (McpHostRejectedException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "MCP server not allowed", detail: ex.Message);
        }
        catch (McpServerStartException ex)
        {
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: "MCP server failed to start", detail: ex.Message);
        }
    }

    /// <summary>
    /// Stops the server and removes its container.
    /// </summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StopAsync(string name) =>
        await host.StopAsync(name) ? NoContent() : NotFound();

    /// <summary>
    /// Whether the server runs, and its recent stderr.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<McpServerStatus>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetStatus(string name) =>
        host.GetStatus(name) is { } status ? Ok(status) : NotFound();

    [HttpPost("tools/list")]
    [ProducesResponseType<ListToolsResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> ListToolsAsync(string name, CancellationToken ct)
    {
        if (Disabled() is { } disabled)
        {
            return disabled;
        }

        try
        {
            var result = await host.ListToolsAsync(name, ct);
            return result is null ? NotFound() : new JsonResult(result, McpJsonUtilities.DefaultOptions);
        }
        catch (Exception ex) when (ex is McpException or McpServerStartException)
        {
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: "MCP server error", detail: ex.Message);
        }
    }

    [HttpPost("tools/call")]
    [ProducesResponseType<CallToolResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> CallToolAsync(string name, [FromBody] JsonElement body, CancellationToken ct)
    {
        if (Disabled() is { } disabled)
        {
            return disabled;
        }

        CallToolRequestParams? request;
        try
        {
            request = body.Deserialize<CallToolRequestParams>(McpJsonUtilities.DefaultOptions);
        }
        catch (JsonException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid tool call", detail: ex.Message);
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Name))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid tool call", detail: "A tool name is required.");
        }

        try
        {
            var result = await host.CallToolAsync(name, request, ct);
            return result is null ? NotFound() : new JsonResult(result, McpJsonUtilities.DefaultOptions);
        }
        catch (Exception ex) when (ex is McpException or McpServerStartException)
        {
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: "MCP server error", detail: ex.Message);
        }
    }

    private ObjectResult? Disabled() => host.Enabled
        ? null
        : Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "MCP host disabled", detail: "The Sandbox MCP host is disabled (Sandbox:Mcp:Enabled).");
}

internal static partial class McpServerNames
{
    public static bool IsValid(string? name) => name is not null && Pattern().IsMatch(name);

    [System.Text.RegularExpressions.GeneratedRegex("^[a-z0-9][a-z0-9-]{1,23}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex Pattern();
}
