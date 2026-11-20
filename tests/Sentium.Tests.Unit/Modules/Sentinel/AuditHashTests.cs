using Sentium.Sentinel.Features.Audit;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Sentinel;

public sealed class AuditHashTests
{
    [Fact]
    public void Of_ReturnsDeterministicLowerHexSha256()
    {
        const string input = "What is the capital of France?";

        var hash = AuditHash.Of(input);

        Assert.Equal(AuditHash.Of(input), hash);
        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]+$", hash);
        Assert.NotEqual(hash, AuditHash.Of("other"));
    }
}
