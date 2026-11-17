namespace Sentium.AppHost.Config;

/// <summary>
/// The S3-compatible object store container. Mirrors the <c>storage</c> service in <c>docker-compose.yml</c>.
/// </summary>
internal static class StorageConfig
{
    public const string Image = "chrislusf/seaweedfs";

    // Pinned by digest. After an upgrade, run the S3 contract tests (ObjectStoreContractTests) and check /healthz.
    public const string Tag = "4.54";
    public const string Digest = "4b42c212543a0acd4c0e675215ecdcaf62a186ef98def76c7e12701871222d15";

    public const int S3Port = 8333;
    public const string HealthPath = "/healthz";
    public const string DataPath = "/data";

    public const string AccessKeyEnv = "AWS_ACCESS_KEY_ID";
    public const string SecretKeyEnv = "AWS_SECRET_ACCESS_KEY";

    /// <summary>
    /// Telemetry and every front end besides the authenticated S3 API are off.
    /// </summary>
    public static readonly string[] Args =
    [
        "mini",
        $"-dir={DataPath}",
        "-ip=127.0.0.1",
        "-ip.bind=0.0.0.0",
        "-master.telemetry=false",
        "-admin.ui=false",
        "-webdav=false",
        "-s3.port.iceberg=0",
        "-s3.port.lance=0",
    ];
}
