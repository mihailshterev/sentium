using Microsoft.AspNetCore.Mvc;
using Sentium.Infrastructure.Results;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Features.Mcp;

namespace Sentium.Praxis.Controllers;

/// <summary>
/// MCP (Model Context Protocol) servers whose tools agents may use. Sovereign-only: registering a server and approving
/// its tools decides what every user's agents can be given.
/// </summary>
[ApiController]
[AuthorizeSovereign]
[Route($"{PraxisRoutes.Prefix}/mcp/servers")]
public sealed class McpServersController(IMcpServerService servers) : ControllerBase
{
    /// <summary>
    /// Every registered MCP server with its tools and connection state.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<McpServerDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) => Ok(await servers.GetAllAsync(ct));

    /// <summary>
    /// One MCP server.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(McpServerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        await servers.GetAsync(id, ct) is { } server ? Ok(server) : NotFound();

    /// <summary>
    /// Registers an MCP server. Its tools are discovered with <c>POST {id}/sync</c> and must be approved before agents
    /// can use them.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(McpServerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateMcpServerRequest request, CancellationToken ct)
    {
        var result = await servers.CreateAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value)
            : result.ToActionResult(this);
    }

    /// <summary>
    /// Updates an MCP server. The name and transport are fixed; omitted secrets are kept.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(McpServerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMcpServerRequest request, CancellationToken ct) =>
        (await servers.UpdateAsync(id, request, ct)).ToActionResult(this);

    /// <summary>
    /// Deletes an MCP server and removes its tools from every agent.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await servers.DeleteAsync(id, ct) ? NoContent() : NotFound();

    /// <summary>
    /// Connects to the server and refreshes its tool list. New tools start disabled and unapproved; changed tools need
    /// re-approval before agents see them again.
    /// </summary>
    [HttpPost("{id:guid}/sync")]
    [ProducesResponseType(typeof(McpServerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Sync(Guid id, CancellationToken ct) =>
        (await servers.SyncAsync(id, ct)).ToActionResult(this);

    /// <summary>
    /// Enables, approves, or changes the approval requirement of the server's tools.
    /// </summary>
    [HttpPut("{id:guid}/tools")]
    [ProducesResponseType(typeof(McpServerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTools(Guid id, [FromBody] UpdateMcpToolsRequest request, CancellationToken ct) =>
        (await servers.UpdateToolsAsync(id, request, ct)).ToActionResult(this);

    /// <summary>
    /// Live connection state and, for Sandbox-hosted servers, the container status and recent stderr.
    /// </summary>
    [HttpGet("{id:guid}/status")]
    [ProducesResponseType(typeof(McpServerStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Status(Guid id, CancellationToken ct) =>
        await servers.GetStatusAsync(id, ct) is { } status ? Ok(status) : NotFound();
}
