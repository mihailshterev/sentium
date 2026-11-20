using Sentium.Memoria.Common;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Memoria;

public sealed class VaultPathsTests
{
    [Theory]
    [InlineData("Notes/Today.md", "Notes/Today.md")]
    [InlineData("Notes\\Today.MD", "Notes/Today.MD")]
    [InlineData("  Top.md ", "Top.md")]
    public void Normalize_AcceptsRelativeMarkdownPaths(string raw, string expected)
    {
        Assert.Equal(expected, VaultPaths.Normalize(raw));
    }

    [Theory]
    [InlineData("/etc/passwd.md")]
    [InlineData("../outside.md")]
    [InlineData("a/../../b.md")]
    [InlineData("C:/vault/a.md")]
    [InlineData("a//b.md")]
    [InlineData("image.png")]
    [InlineData(".md")]
    [InlineData("")]
    public void Normalize_RejectsUnsafeOrNonNotePaths(string raw)
    {
        Assert.Null(VaultPaths.Normalize(raw));
    }

    [Fact]
    public void ToKey_LowerCasesAndDropsTheExtension()
    {
        Assert.Equal("areas/health", VaultPaths.ToKey("Areas/Health.md"));
    }

    [Theory]
    [InlineData("a/b", "../c.md", "a/c.md")]
    [InlineData("a", "./b/c.md", "a/b/c.md")]
    [InlineData("", "../c.md", null)]
    public void Combine_ResolvesRelativeLinks(string folder, string relative, string? expected)
    {
        Assert.Equal(expected, VaultPaths.Combine(folder, relative));
    }
}
