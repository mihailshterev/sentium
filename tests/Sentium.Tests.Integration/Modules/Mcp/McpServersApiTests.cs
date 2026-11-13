using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Sentium.Mcp.Entities;
using Sentium.Mcp.Features.Runtime;
using Sentium.Mcp.Features.Servers;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Entities;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Tools;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Mcp;

/// <summary>
/// End to end against a real Streamable HTTP MCP server (the MCP C# SDK's ASP.NET Core host on loopback): register,
/// discover, approve, assign to an agent, invoke through the agent tool surface, and delete.
/// </summary>
public sealed class McpServersApiTests(MonolithWebApplicationFactory factory)
    : IClassFixture<MonolithWebApplicationFactory>, IAsyncLifetime
{
    private const string BaseUrl = "/api/praxis/mcp/servers";

    private readonly HttpClient _client = factory.CreateClient();
    private readonly ConcurrentQueue<string?> _authorizationHeaders = new();
    private WebApplication? _mcpServer;
    private string _mcpEndpoint = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        builder.Services.AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .WithTools(
            [
                McpServerTool.Create(
                    (string text) => $"echo: {text}",
                    new McpServerToolCreateOptions { Name = "echo", Description = "Echoes the text back.", ReadOnly = true }),
                McpServerTool.Create(
                    () => "deleted",
                    new McpServerToolCreateOptions { Name = "delete_everything", Description = "Deletes everything.", Destructive = true }),
            ]);

        _mcpServer = builder.Build();
        _mcpServer.Use(async (context, next) =>
        {
            _authorizationHeaders.Enqueue(context.Request.Headers.Authorization.ToString());
            await next(context);
        });
        _mcpServer.MapMcp("/mcp");

        await _mcpServer.StartAsync(Ct);

        var address = _mcpServer.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        _mcpEndpoint = $"{address}/mcp";
    }

    public async ValueTask DisposeAsync()
    {
        if (_mcpServer is not null)
        {
            await _mcpServer.DisposeAsync();
        }
    }

    [Fact]
    public async Task Register_Sync_Approve_Assign_Invoke_Delete()
    {
        var name = $"t{Guid.NewGuid():N}"[..12];

        // Register - the secret header is stored encrypted and only its name comes back.
        var created = await _client.PostAsJsonAsync(BaseUrl, new CreateMcpServerRequest(
            name, "Echo server", null, McpTransport.Http, _mcpEndpoint, null, null, null, null, null,
            new Dictionary<string, string> { ["Authorization"] = "Bearer test-token" }), Ct);

        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        var server = (await created.Content.ReadFromJsonAsync<McpServerDto>(Ct))!;
        server.SecretNames.Should().Equal("Authorization");
        (await created.Content.ReadAsStringAsync(Ct)).Should().NotContain("test-token");

        // Discover - new tools start disabled and unapproved, so agents cannot see them yet.
        var synced = await _client.PostAsync($"{BaseUrl}/{server.Id}/sync", null, Ct);
        synced.StatusCode.Should().Be(HttpStatusCode.OK, await synced.Content.ReadAsStringAsync(Ct));
        server = (await synced.Content.ReadFromJsonAsync<McpServerDto>(Ct))!;

        server.Tools.Select(t => t.QualifiedName).Should().BeEquivalentTo([$"{name}__echo", $"{name}__delete_everything"]);
        server.Tools.Should().OnlyContain(t => !t.Enabled && !t.Approved && !t.Usable);
        server.Tools.Single(t => t.Name == "echo").ReadOnlyHint.Should().BeTrue();
        _authorizationHeaders.Should().Contain("Bearer test-token", "the decrypted secret header reaches the MCP server");

        (await GetToolNamesAsync()).Should().NotContain($"{name}__echo");

        // Approve and enable only the read-only tool.
        var approved = await _client.PutAsJsonAsync($"{BaseUrl}/{server.Id}/tools",
            new UpdateMcpToolsRequest([new McpToolUpdate("echo", Enabled: true, Approve: true)]), Ct);
        approved.StatusCode.Should().Be(HttpStatusCode.OK);

        var catalogue = await _client.GetFromJsonAsync<List<ToolDescriptor>>("/api/praxis/tools", Ct);
        var echo = catalogue!.Should().ContainSingle(t => t.Name == $"{name}__echo").Subject;
        echo.Server.Should().Be("Echo server");
        echo.Resource.Should().Be("External");
        echo.RequiresApproval.Should().BeTrue("approval is required by default");
        catalogue.Should().NotContain(t => t.Name == $"{name}__delete_everything");

        // Assignable to an agent (the validator knows MCP names) ...
        var agentResponse = await _client.PostAsJsonAsync("/api/praxis/agents",
            new CreateAgentRequest($"MCP agent {name}", "Uses the echo server.", Tools: [$"{name}__echo"]), Ct);
        agentResponse.StatusCode.Should().Be(HttpStatusCode.Created, await agentResponse.Content.ReadAsStringAsync(Ct));
        var agent = (await agentResponse.Content.ReadFromJsonAsync<AgentResponse>(Ct))!;

        // ... and callable through the agent tool surface (the function an agent would get).
        var function = factory.Services.GetRequiredService<IMcpToolCatalog>()
            .GetFunctions(new HashSet<string> { $"{name}__echo" })
            .Should().ContainSingle().Subject;
        function.GetService<ApprovalRequiredAIFunction>().Should().NotBeNull();

        var result = await function.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["text"] = "hi" }), Ct);
        result.Should().Be("echo: hi");

        // Deleting the server removes its tools from the catalogue and from the agent.
        (await _client.DeleteAsync($"{BaseUrl}/{server.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await GetToolNamesAsync()).Should().NotContain($"{name}__echo");
        var reloaded = await _client.GetFromJsonAsync<AgentResponse>($"/api/praxis/agents/{agent.Id}", Ct);
        reloaded!.Tools.Should().NotContain($"{name}__echo");
    }

    [Fact]
    public async Task Variables_AreReturnedInPlainText_And_MoveBetweenSecretAndPlain()
    {
        var name = $"v{Guid.NewGuid():N}"[..12];

        var created = await _client.PostAsJsonAsync(BaseUrl, new CreateMcpServerRequest(
            name, "Variables server", null, McpTransport.Http, _mcpEndpoint, null, null, null, null, null,
            new Dictionary<string, string> { ["Authorization"] = "Bearer secret-value" },
            Variables: new Dictionary<string, string> { ["X-Region"] = "eu" }), Ct);

        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        var server = (await created.Content.ReadFromJsonAsync<McpServerDto>(Ct))!;
        server.SecretNames.Should().Equal("Authorization");
        server.Variables.Should().Equal(new Dictionary<string, string> { ["X-Region"] = "eu" });
        (await created.Content.ReadAsStringAsync(Ct)).Should().NotContain("secret-value");

        // Make the secret plain text and edit the other variable; the name moves instead of being duplicated.
        var updated = await _client.PutAsJsonAsync($"{BaseUrl}/{server.Id}", new UpdateMcpServerRequest(
            "Variables server", null, _mcpEndpoint, null, null, null, null, null, null, true,
            new Dictionary<string, string?> { ["Authorization"] = "Bearer now-visible", ["X-Region"] = "us" }), Ct);

        updated.StatusCode.Should().Be(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync(Ct));
        server = (await updated.Content.ReadFromJsonAsync<McpServerDto>(Ct))!;
        server.SecretNames.Should().BeEmpty();
        server.Variables.Should().Equal(new Dictionary<string, string> { ["Authorization"] = "Bearer now-visible", ["X-Region"] = "us" });

        // Plain variables are sent to the server just like secrets.
        (await _client.PostAsync($"{BaseUrl}/{server.Id}/sync", null, Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        _authorizationHeaders.Should().Contain("Bearer now-visible");

        // A name cannot be both.
        var both = await _client.PutAsJsonAsync($"{BaseUrl}/{server.Id}", new UpdateMcpServerRequest(
            "Variables server", null, _mcpEndpoint, null, null, null, null, null,
            new Dictionary<string, string?> { ["X-Region"] = "x" }, true,
            new Dictionary<string, string?> { ["X-Region"] = "y" }), Ct);
        both.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await _client.DeleteAsync($"{BaseUrl}/{server.Id}", Ct);
    }

    [Fact]
    public async Task PublicEndpoint_IsRejected()
    {
        var response = await _client.PostAsJsonAsync(BaseUrl, new CreateMcpServerRequest(
            "public", "Public", null, McpTransport.Http, "http://8.8.8.8/mcp", null, null, null, null, null, null), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AgentWithUnknownMcpTool_IsRejected()
    {
        var response = await _client.PostAsJsonAsync("/api/praxis/agents",
            new CreateAgentRequest($"Agent {Guid.NewGuid():N}", "x", Tools: ["nosuch__tool"]), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NonSovereign_CannotManageMcpServers()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl);
        request.Headers.Add(TestAuthHandler.UserHeader, Guid.NewGuid().ToString());

        var response = await _client.SendAsync(request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<List<string>> GetToolNamesAsync() =>
        [.. (await _client.GetFromJsonAsync<List<ToolDescriptor>>("/api/praxis/tools", Ct))!.Select(t => t.Name)];
}
