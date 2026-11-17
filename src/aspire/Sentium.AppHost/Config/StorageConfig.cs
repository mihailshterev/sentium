namespace Sentium.AppHost.Config;

/// <summary>
/// SeaweedFS in single-process <c>weed mini</c> mode as the S3-compatible object store. Mirrors the <c>storage</c>
/// service in <c>docker-compose.yml</c>; see ADR 0005.
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

    /// <summary>
    /// Not "data": dev machines may still have the <c>storage-data</c> volume of the Azurite emulator this replaced,
    /// and SeaweedFS must not start on top of its files.
    /// </summary>
    public const string VolumeSuffix = "seaweedfs-data";

    public const string AccessKeyEnv = "AWS_ACCESS_KEY_ID";
    public const string SecretKeyEnv = "AWS_SECRET_ACCESS_KEY";

    /// <summary>
    /// One process (master, volume, filer, S3 gateway) with telemetry and every extra front end off: only the
    /// authenticated S3 API is used.
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
