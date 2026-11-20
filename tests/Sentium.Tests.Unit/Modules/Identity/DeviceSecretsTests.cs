using Sentium.Identity.Features.Devices;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Identity;

public sealed class DeviceSecretsTests
{
    [Fact]
    public void NewPairingCode_IsEightCharacters_WithoutAmbiguousOnes()
    {
        var codes = Enumerable.Range(0, 200).Select(_ => DeviceSecrets.NewPairingCode()).ToList();

        Assert.All(codes, c => Assert.True(c.Length == DeviceLimits.PairingCodeLength));
        Assert.DoesNotMatch("[01OIL]", string.Concat(codes));
        Assert.True(codes.Distinct().Count() > 190, "codes are random");
    }

    [Theory]
    [InlineData("abcd-efgh", "ABCDEFGH")]
    [InlineData(" ABCD EFGH ", "ABCDEFGH")]
    [InlineData("a2c4e6g8", "A2C4E6G8")]
    public void NormalizePairingCode_IgnoresCaseAndSeparators(string typed, string expected)
    {
        Assert.Equal(expected, DeviceSecrets.NormalizePairingCode(typed));
    }

    [Fact]
    public void NewToken_Is32RandomBytes_UrlSafe()
    {
        var first = DeviceSecrets.NewToken();
        var second = DeviceSecrets.NewToken();

        // 32 bytes are 43 unpadded base64url characters
        Assert.Equal(43, first.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", first);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Hash_IsStableHexSha256_AndDiffersPerSecret()
    {
        var hash = DeviceSecrets.Hash("secret");

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9A-F]+$", hash);
        Assert.Equal(hash, DeviceSecrets.Hash("secret"));
        Assert.NotEqual(hash, DeviceSecrets.Hash("Secret"));
    }

    [Theory]
    [InlineData("SentiumSatelles/1.2.0", "1.2.0")]
    [InlineData("SentiumSatelles/1.2.0-beta+7 (Windows 11)", "1.2.0-beta+7")]
    [InlineData("Mozilla/5.0 sentiumsatelles/0.1", "0.1")]
    public void ParseAppVersion_ReadsTheSatellesProduct(string userAgent, string expected)
    {
        Assert.Equal(expected, DeviceUserAgent.ParseAppVersion(userAgent));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Mozilla/5.0")]
    [InlineData("SentiumSatelles/")]
    [InlineData("SentiumSatelles/<script>")]
    [InlineData("SentiumSatelles/123456789012345678901234567890123")]
    public void ParseAppVersion_ReturnsNull_ForAnythingElse(string? userAgent)
    {
        Assert.Null(DeviceUserAgent.ParseAppVersion(userAgent));
    }
}
