using System.Data.Common;

namespace Sentium.Infrastructure.Storage;

/// <summary>
/// The S3-compatible endpoint and credentials, from a connection string of the form
/// <c>Endpoint=http://storage:8333;AccessKey=...;SecretKey=...</c> with an optional <c>Region</c>.
/// </summary>
public sealed record ObjectStoreSettings(Uri Endpoint, string AccessKey, string SecretKey, string Region)
{
    /// <summary>
    /// Self-hosted S3 servers ignore the region, but SigV4 signs it, so both sides must agree on one.
    /// </summary>
    public const string DefaultRegion = "us-east-1";

    private const string EndpointKey = "Endpoint";
    private const string AccessKeyKey = "AccessKey";
    private const string SecretKeyKey = "SecretKey";
    private const string RegionKey = "Region";

    /// <summary>
    /// Error messages name the missing or invalid part but never echo the connection string, which holds the secret.
    /// </summary>
    public static ObjectStoreSettings Parse(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("The object storage connection string is not set.");
        }

        var parts = new DbConnectionStringBuilder { ConnectionString = connectionString };

        var endpointValue = Read(parts, EndpointKey);
        if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("The object storage Endpoint must be an absolute http or https URL.");
        }

        var region = parts.TryGetValue(RegionKey, out var regionValue) && regionValue is string { Length: > 0 } r ? r : DefaultRegion;

        return new ObjectStoreSettings(endpoint, Read(parts, AccessKeyKey), Read(parts, SecretKeyKey), region);
    }

    public override string ToString() => $"{EndpointKey}={Endpoint}";

    private static string Read(DbConnectionStringBuilder parts, string key)
    {
        return parts.TryGetValue(key, out var value) && value is string { Length: > 0 } text
            ? text
            : throw new InvalidOperationException($"The object storage connection string has no {key}.");
    }
}
