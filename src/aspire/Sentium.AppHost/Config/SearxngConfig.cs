namespace Sentium.AppHost.Config;

internal static class SearxngConfig
{
    public const string Image = "searxng/searxng";
    public const string Digest = "7add5ffe7933e6b46aa3a26bed8a0c342c9a45ec331d9a8cf145e46b04b24f36";
    public const int ContainerPort = 8080;
    public const string SettingsSource = "../../../infra/searxng/settings.yml";
    public const string SettingsPath = "/etc/searxng/settings.yml";
    public const string EgressNetwork = "sentium-egress";
    public const string EgressNetworkKey = "Mcp:AllowedNetwork";
}
