using System.Text;
using FluentAssertions;
using Sentium.Locus.Features.Attachments;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Locus;

public sealed class AttachmentPolicyTests
{
    public static TheoryData<byte[], string, string> AllowedFiles => new()
    {
        { [0xFF, 0xD8, 0xFF, 0xE0, 0x00], "photo.jpg", "image/jpeg" },
        { [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00], "photo.png", "image/png" },
        { Encoding.ASCII.GetBytes("GIF89a......"), "anim.gif", "image/gif" },
        { Encoding.ASCII.GetBytes("RIFF\0\0\0\0WEBPVP8 "), "photo.webp", "image/webp" },
        { Encoding.ASCII.GetBytes("\0\0\0\u0018ftypheic\0\0\0\0"), "photo.heic", "image/heic" },
        { Encoding.ASCII.GetBytes("%PDF-1.7\n"), "manual.pdf", "application/pdf" },
        { Encoding.UTF8.GetBytes("Receipt: 12.50 EUR"), "receipt.txt", "text/plain" },
    };

    [Theory]
    [MemberData(nameof(AllowedFiles))]
    public void DetectContentType_RecognisesAllowedFormatsFromTheirBytes(byte[] header, string fileName, string expected)
    {
        AttachmentPolicy.DetectContentType(header, fileName).Should().Be(expected);
    }

    [Fact]
    public void DetectContentType_IgnoresTheFileExtensionForBinaryFormats()
    {
        // A PNG named .pdf is still a PNG; the client's claim never decides the stored type.
        AttachmentPolicy.DetectContentType([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], "fake.pdf").Should().Be("image/png");
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>", "logo.svg")]
    [InlineData("<html><script>alert(1)</script></html>", "page.html")]
    [InlineData("MZ\u0090\0", "setup.exe")]
    public void DetectContentType_RejectsActiveOrUnknownFormats(string content, string fileName)
    {
        AttachmentPolicy.DetectContentType(Encoding.UTF8.GetBytes(content), fileName).Should().BeNull();
    }

    [Fact]
    public void DetectContentType_RejectsBinaryContentDisguisedAsText()
    {
        AttachmentPolicy.DetectContentType([0x41, 0x00, 0x42, 0x43], "notes.txt").Should().BeNull();
    }

    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("C:\\Users\\me\\receipt.pdf", "receipt.pdf")]
    [InlineData("  ", "attachment")]
    [InlineData(null, "attachment")]
    [InlineData("bad\u0007name.png", "badname.png")]
    public void SanitizeFileName_StripsPathsAndControlCharacters(string? input, string expected)
    {
        AttachmentPolicy.SanitizeFileName(input).Should().Be(expected);
    }

    [Fact]
    public void SanitizeFileName_CapsLengthButKeepsTheExtension()
    {
        var name = AttachmentPolicy.SanitizeFileName(new string('a', 400) + ".pdf");

        name.Should().HaveLength(255).And.EndWith(".pdf");
    }
}
