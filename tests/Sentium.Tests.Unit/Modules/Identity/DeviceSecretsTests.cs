using FluentAssertions;
using Sentium.Identity.Features.Devices;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Identity;

public sealed class DeviceSecretsTests
{
    [Fact]
    public void NewPairingCode_IsEightCharacters_WithoutAmbiguousOnes()
    {
        var codes = Enumerable.Range(0, 200).Select(_ => DeviceSecrets.NewPairingCode()).ToList();

        codes.Should().OnlyContain(c => c.Length == DeviceLimits.PairingCodeLength);
        string.Concat(codes).Should().NotContainAny("0", "O", "1", "I", "L");
        codes.Distinct().Should().HaveCountGreaterThan(190, "codes are random");
    }

    [Theory]
    [InlineData("abcd-efgh", "ABCDEFGH")]
    [InlineData(" ABCD EFGH ", "ABCDEFGH")]
    [InlineData("a2c4e6g8", "A2C4E6G8")]
    public void NormalizePairingCode_IgnoresCaseAndSeparators(string typed, string expected)
    {
        DeviceSecrets.NormalizePairingCode(typed).Should().Be(expected);
    }

    [Fact]
    public void NewToken_Is32RandomBytes_UrlSafe()
    {
        var first = DeviceSecrets.NewToken();
        var second = DeviceSecrets.NewToken();

        first.Should().HaveLength(43, "32 bytes are 43 unpadded base64url characters");
        first.Should().MatchRegex("^[A-Za-z0-9_-]+$");
        second.Should().NotBe(first);
    }

    [Fact]
    public void Hash_IsStableHexSha256_AndDiffersPerSecret()
    {
        var hash = DeviceSecrets.Hash("secret");

        hash.Should().HaveLength(64).And.MatchRegex("^[0-9A-F]+$");
        DeviceSecrets.Hash("secret").Should().Be(hash);
        DeviceSecrets.Hash("Secret").Should().NotBe(hash);
    }

    [Theory]
    [InlineData("SentiumSatelles/1.2.0", "1.2.0")]
    [InlineData("SentiumSatelles/1.2.0-beta+7 (Windows 11)", "1.2.0-beta+7")]
    [InlineData("Mozilla/5.0 sentiumsatelles/0.1", "0.1")]
    public void ParseAppVersion_ReadsTheSatellesProduct(string userAgent, string expected)
    {
        DeviceUserAgent.ParseAppVersion(userAgent).Should().Be(expected);
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
        DeviceUserAgent.ParseAppVersion(userAgent).Should().BeNull();
    }
}
