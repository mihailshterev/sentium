using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Sentium.Mcp.Features.Servers;

/// <summary>
/// Protects MCP server secrets (HTTP headers / container environment) at rest with ASP.NET Core Data Protection.
/// The protected payload is a JSON map; plaintext never leaves the process except to the MCP server it belongs to.
/// </summary>
internal sealed class McpSecretProtector(IDataProtectionProvider provider)
{
    // Deliberately still "Sentium.Praxis...": the Data Protection purpose keys the stored secrets, so renaming it after
    // MCP moved out of Praxis would make every existing server secret undecryptable.
    private const string Purpose = "Sentium.Praxis.Mcp.Secrets.v1";

    private readonly IDataProtector protector = provider.CreateProtector(Purpose);

    public string? Protect(IReadOnlyDictionary<string, string> secrets)
    {
        ArgumentNullException.ThrowIfNull(secrets);

        return secrets.Count == 0 ? null : protector.Protect(JsonSerializer.Serialize(secrets));
    }

    /// <summary>
    /// The secrets, or an empty map when none are stored. Throws <see cref="CryptographicException"/> when the payload
    /// cannot be decrypted (e.g. the key ring was lost) - callers surface that as a server error to fix in the portal.
    /// </summary>
    public IReadOnlyDictionary<string, string> Unprotect(string? protectedSecrets)
    {
        if (string.IsNullOrEmpty(protectedSecrets))
        {
            return new Dictionary<string, string>();
        }

        var json = protector.Unprotect(protectedSecrets);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
    }

    /// <summary>
    /// Names of the stored secrets (values stay encrypted), or an empty list when they cannot be decrypted.
    /// </summary>
    public IReadOnlyList<string> GetNames(string? protectedSecrets)
    {
        try
        {
            return [.. Unprotect(protectedSecrets).Keys.Order(StringComparer.Ordinal)];
        }
        catch (CryptographicException)
        {
            return [];
        }
    }
}
