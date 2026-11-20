using System.Text;
using Sentium.Identity.Features.Avatars;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Identity;

public sealed class AvatarPolicyTests
{
    public static TheoryData<byte[], string> AllowedPictures => new()
    {
        { [0xFF, 0xD8, 0xFF, 0xE0, 0x00], "image/jpeg" },
        { [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00], "image/png" },
        { Encoding.ASCII.GetBytes("RIFF\0\0\0\0WEBPVP8 "), "image/webp" },
    };

    [Theory]
    [MemberData(nameof(AllowedPictures))]
    public void DetectContentType_RecognisesCanvasFormats(byte[] header, string expected)
    {
        Assert.Equal(expected, AvatarPolicy.DetectContentType(header));
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>")]
    [InlineData("GIF89a......")]
    [InlineData("\0\0\0\u0018ftypheic\0\0\0\0")]
    [InlineData("%PDF-1.7\n")]
    [InlineData("RIFF\0\0\0\0WAVEfmt ")]
    public void DetectContentType_RejectsEverythingElse(string content)
    {
        Assert.Null(AvatarPolicy.DetectContentType(Encoding.ASCII.GetBytes(content)));
    }

    [Fact]
    public void AvatarUrl_IsVersionedAndAbsentWithoutAPicture()
    {
        var userId = Guid.Parse("8f1c2c55-0000-0000-0000-000000000001");
        var updatedAt = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_123);

        Assert.Equal($"/api/identity/users/{userId}/avatar?v=1700000000123", AvatarUrls.For(userId, updatedAt));
        Assert.Null(AvatarUrls.For(userId, null));
    }
}
