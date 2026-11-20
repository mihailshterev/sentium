using System.ComponentModel.DataAnnotations;
using Sentium.Core.Constants;

namespace Sentium.Registry.Contracts.Settings;

/// <summary>
/// Global settings for the MCP (Model Context Protocol) client, managed by Sovereign users and consumed by Praxis.
/// The servers themselves are Praxis data (registered and approved in the portal); these are the runtime knobs.
/// </summary>
[SettingsGroup(
    SettingsKeys.Mcp,
    SettingsScope.Household,
    "mcp",
    "MCP",
    "Client limits for every MCP server and every user.",
    Order = 4)]
public sealed class McpSettings
{
    [Setting(
        "Enable MCP",
        "Kill switch. When off, no agent is offered an MCP tool and open MCP connections are closed.",
        Keywords = ["model context protocol", "tools", "kill switch"])]
    public bool Enabled { get; set; } = true;

    [Setting(
        "Connect timeout",
        "Time allowed to connect to and initialize an MCP server.",
        Keywords = ["model context protocol", "timeout"],
        Unit = "s")]
    [Range(1, 300)]
    public int ConnectTimeoutSeconds { get; set; } = 30;

    [Setting(
        "Call timeout",
        "Time allowed for a single MCP tool call.",
        Keywords = ["model context protocol", "timeout", "tool"],
        Unit = "s")]
    [Range(1, 600)]
    public int CallTimeoutSeconds { get; set; } = 60;

    [Setting(
        "Idle timeout",
        "An unused MCP connection is closed after this long, and a Sandbox-hosted server container is stopped with it.",
        Keywords = ["model context protocol", "timeout", "sandbox", "container"],
        Unit = "min")]
    [Range(1, 1_440)]
    public int IdleTimeoutMinutes { get; set; } = 15;

    [Setting(
        "Max result size",
        "Longer tool results are truncated before they enter the conversation.",
        Keywords = ["model context protocol", "truncate", "characters", "context"],
        Unit = "chars")]
    [Range(1_000, 200_000)]
    public int MaxResultChars { get; set; } = 16_000;
}
