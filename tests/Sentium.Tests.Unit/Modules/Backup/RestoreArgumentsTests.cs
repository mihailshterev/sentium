using FluentAssertions;
using Sentium.Backup.Restore;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Backup;

public sealed class RestoreArgumentsTests
{
    [Fact]
    public void Restore_WithIdentityAndReplace()
    {
        RestoreArguments.Parse(["restore", "/backups/a.tar.age", "--identity", "/id.txt", "--replace"])
            .Should().Be(new RestoreArguments("/backups/a.tar.age", "/id.txt", Replace: true, VerifyOnly: false));
    }

    [Fact]
    public void Verify_IsVerifyOnly()
    {
        RestoreArguments.Parse(["verify", "/backups/a.tar"])
            .Should().Be(new RestoreArguments("/backups/a.tar", null, Replace: false, VerifyOnly: true));
    }

    [Theory]
    [InlineData("restore")]
    [InlineData("restore", "/a.tar", "--identity")]
    [InlineData("restore", "/a.tar", "--force")]
    [InlineData("verify", "/a.tar", "--replace")]
    [InlineData("backup", "/a.tar")]
    public void InvalidArguments_AreRejected(params string[] args)
    {
        RestoreArguments.Parse(args).Should().BeNull();
    }

    [Fact]
    public void NoArguments_StartTheService()
    {
        RestoreArguments.IsCommand([]).Should().BeFalse();
    }
}
