using FluentAssertions;
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
        VaultPaths.Normalize(raw).Should().Be(expected);
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
        VaultPaths.Normalize(raw).Should().BeNull();
    }

    [Fact]
    public void ToKey_LowerCasesAndDropsTheExtension()
    {
        VaultPaths.ToKey("Areas/Health.md").Should().Be("areas/health");
    }

    [Theory]
    [InlineData("a/b", "../c.md", "a/c.md")]
    [InlineData("a", "./b/c.md", "a/b/c.md")]
    [InlineData("", "../c.md", null)]
    public void Combine_ResolvesRelativeLinks(string folder, string relative, string? expected)
    {
        VaultPaths.Combine(folder, relative).Should().Be(expected);
    }
}
