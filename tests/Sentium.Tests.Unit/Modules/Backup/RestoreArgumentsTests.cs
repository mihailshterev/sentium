using Sentium.Backup.Restore;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Backup;

public sealed class RestoreArgumentsTests
{
    [Fact]
    public void Restore_WithIdentityAndReplace()
    {
        Assert.Equal(new RestoreArguments("/backups/a.tar.age", "/id.txt", Replace: true, VerifyOnly: false), RestoreArguments.Parse(["restore", "/backups/a.tar.age", "--identity", "/id.txt", "--replace"]));
    }

    [Fact]
    public void Verify_IsVerifyOnly()
    {
        Assert.Equal(new RestoreArguments("/backups/a.tar", null, Replace: false, VerifyOnly: true), RestoreArguments.Parse(["verify", "/backups/a.tar"]));
    }

    [Theory]
    [InlineData("restore")]
    [InlineData("restore", "/a.tar", "--identity")]
    [InlineData("restore", "/a.tar", "--force")]
    [InlineData("verify", "/a.tar", "--replace")]
    [InlineData("backup", "/a.tar")]
    public void InvalidArguments_AreRejected(params string[] args)
    {
        Assert.Null(RestoreArguments.Parse(args));
    }

    [Fact]
    public void NoArguments_StartTheService()
    {
        Assert.False(RestoreArguments.IsCommand([]));
    }
}
